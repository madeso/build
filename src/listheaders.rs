use std::path::{Path, PathBuf};
use structopt::StructOpt;
use thiserror::Error;
use std::io;
use std::fmt;
use std::collections::{HashMap, HashSet};

use counter;
use regex::Regex;

use crate::
{
    builddata,
    core,
    printer,
    compilecommands
};


#[derive(StructOpt, Debug)]
pub struct LinesArg
{
    /// File to list lines in
    #[structopt(parse(from_os_str))]
    filename: PathBuf,
    
    /// List statements instead
    #[structopt(long)]
    statements: bool,
    
    /// List blocks instead
    #[structopt(long)]
    blocks: bool
}


#[derive(StructOpt, Debug)]
pub struct FilesArg
{
    /// project file
    #[structopt(parse(from_os_str))]
    sources: Vec<PathBuf>,
    
    /// number of most common includes to print
    #[structopt(default_value="10", long)]
    count: usize,
    
    // nargs="*", default=[])
    /// folders to exclude
    #[structopt(long)]
    exclude : Vec<PathBuf>,
    
    /// print debug info
    #[structopt(long)]
    debug: bool,
    
    #[structopt(flatten)]
    cc: compilecommands::CompileCommandArg
}


/// Tool to list headers
#[derive(StructOpt, Debug)]
pub enum Options
{
    /// List lines in a single file
    Lines
    {
        #[structopt(flatten)]
        lines: LinesArg
    },
    
    /// Display includeded files from one or more source files
    Files
    {
        #[structopt(flatten)]
        files: FilesArg
    }
}


#[derive(Error, Debug)]
enum Fail
{
    #[error(transparent)]
    Io(#[from] io::Error)
}


fn join_lines(lines: Vec<String>) -> Vec<String>
{
    let mut r = Vec::<String>::new();
    let mut last_line : Option<String> = None;
    
    for line in lines
    {
        if line.ends_with("\\")
        {
            let without = &line[..line.len() - 1];
            last_line = match last_line
            {
                None => Some(without.to_string()),
                Some(str) => Some(str + without)
            };
        }
        else
        {
            if let Some(str) = last_line
            {
                r.push(str + &line);
                last_line = None;
            }
            else
            {
                r.push(line.to_string());
            }
        }
    }

    if let Some(str) = last_line
    {
        r.push(str);
    }

    r
}

struct Line
{
    text: String,
    line: usize
}

struct CommentStripper
{
    ret: Vec<Line>,
    mem: String,
    last: char,
    single_line_comment: bool,
    multi_line_comment: bool,
    line: usize
}

impl CommentStripper
{
    fn add_last(&mut self)
    {
        if self.last != '\0'
        {
            self.mem.push(self.last);
        }
        self.last = '\0';
    }

    fn complete(&mut self)
    {
        self.add_last();
        if self.mem.is_empty() == false
        {
            self.add_mem();
        }
    }

    fn add_mem(&mut self)
    {
        self.ret.push(Line{text: self.mem.to_string(), line: self.line});
        self.mem = "".to_string();
    }

    fn add(&mut self, c: char)
    {
        let last = self.last;
        if c != '\n'
        {
            self.last = c;
        }

        if c == '\n'
        {
            self.add_last();
            self.add_mem();
            self.line += 1;
            self.single_line_comment = false;
            return;
        }
        if self.single_line_comment
        {
            return;
        }
        if self.multi_line_comment
        {
            if last == '*' && c == '/'
            {
                self.multi_line_comment = false;
                return;
            }
        }
        if last == '/' && c == '/'
        {
            self.single_line_comment = true
        }

        if last == '/' && c == '*'
        {
            self.multi_line_comment = true;
            return;
        }

        self.add_last();
    }
}

fn remove_cpp_comments(lines: Vec<String>) -> Vec<Line>
{
    let mut cs = CommentStripper
    {
        ret: Vec::<Line>::new(),
        mem: "".to_string(),
        last: '\0',
        single_line_comment: false,
        multi_line_comment: false,
        line: 1
    };
    for line in lines
    {
        for c in line.chars()
        {
            cs.add(c);
        }
        cs.add('\n');
    }

    cs.complete();

    cs.ret
}


#[derive(Debug, Clone)]
struct Preproc
{
    command: String,
    arguments: String,
    line: usize
}

#[derive(Debug)]
struct PreprocParser
{
    commands: Vec<Preproc>,
    index: usize
}

impl PreprocParser
{
    pub fn validate_index(&self) -> bool
    {
        if self.index >= self.commands.len()
        {
            false
        }
        else
        {
            true
        }
    }
    
    pub fn opeek(&self) -> Option<&Preproc>
    {
        if self.validate_index()
        {
            Some(&self.commands[self.index])
        }
        else
        {
            None
        }
    }
    
    pub fn skip(&mut self)
    {
        self.index += 1;
    }
    
    pub fn undo(&mut self)
    {
        self.index -= 1;
    }
    
    // type Item = Preproc;

    fn next(&mut self) -> Option<Preproc>
    {
        if self.validate_index()
        {
            let it = self.index;
            self.index += 1;
            Some(self.commands[it].clone())
        }
        else
        {
            None
        }
    }
}

#[derive(Clone)]
enum Statement
{
    Command(Command),
    Block(Block)
}
impl fmt::Debug for Statement
{
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result
    {
        match self
        {
            Statement::Command(c) => c.fmt(f),
            Statement::Block(b) => b.fmt(f)
        }
    }
}

#[derive(Debug, Clone)]
struct Command
{
    name: String,
    value: String
}

#[derive(Debug, Clone)]
struct Elif
{
    condition: String,
    block: Vec<Statement>
}

#[derive(Debug, Clone)]
struct Block
{
    name: String,
    condition: String,
    true_block: Vec<Statement>,
    false_block: Vec<Statement>,
    elifs: Vec<Elif>
}

fn is_if_start(name: &str) -> bool
{
    name == "if" || name == "ifdef" || name == "ifndef"
}

fn peek_name(commands: &PreprocParser) -> String
{
    match commands.opeek()
    {
        Some(x) => x.command.to_string(),
        None => "".to_string()
    }
}

fn group_commands(path: &Path, print: &mut printer::Printer, ret: &mut Vec<Statement>, commands: &mut PreprocParser, depth: i32)
{
    while let Some(command) = commands.next()
    {
        if is_if_start(&command.command)
        {
            let mut group = Block
            {
                name: command.command,
                condition: command.arguments,
                true_block: Vec::<Statement>::new(),
                false_block: Vec::<Statement>::new(),
                elifs: Vec::<Elif>::new()
            };
            group_commands(path, print, &mut group.true_block, commands, depth+1);
            while peek_name(commands) == "elif"
            {
                let elif_args = commands.next().unwrap().arguments.to_string();
                let mut block = Vec::<Statement>::new();
                group_commands(path, print, &mut block, commands, depth+1);
                group.elifs.push
                (
                    Elif
                    {
                        condition: elif_args,
                        block
                    }
                )
            }
            if peek_name(commands) == "else"
            {
                commands.skip();
                group_commands(path, print, &mut group.false_block, commands, depth+1);
            }
            if peek_name(commands) == "endif"
            {
                commands.skip();
            }
            else
            {

            }
            ret.push(Statement::Block(group));
        }
        else if command.command == "else"
        {
            commands.undo();
            return;
        }
        else if command.command == "endif"
        {
            if depth > 0
            {
                commands.undo();
                return;
            }
            else
            {
                print.error(format!("{}({}): Ignored unmatched endif", path.display(), command.line).as_str());
            }
        }
        else if command.command == "elif"
        {
            if depth > 0
            {
                commands.undo();
                return;
            }
            else
            {
                print.error(format!("{}({}): Ignored unmatched elif", path.display(), command.line).as_str());
            }
        }
        else
        {
            match command.command.as_str()
            {
                "define" | "error" | "include" | "pragma" | "undef" =>
                {
                    ret.push(Statement::Command(Command{
                        name: command.command,
                        value: command.arguments
                    }));
                },
                "version" =>
                {
                    // todo(Gustav): glsl verbatim string, ignore for now
                    // pass
                },
                _ =>
                {
                    print.error(format!("{}({}): unknown pragma {}", path.display(), command.line, command.command).as_str());
                }
            }
        }
    }
}


fn parse_to_statements(lines: Vec<Line>) -> Vec<Preproc>
{
    let mut r = Vec::<Preproc>::new();

    for line in lines
    {
        if line.text.starts_with("#")
        {
            let li = line.text[1..].trim_start();
            let (command, arguments) = ident_split(li);

            r.push
            (
                Preproc
                {
                    command,
                    arguments,
                    line: line.line
                }
            )
        }
    }

    r
}

fn parse_to_blocks(path: &Path, print: &mut printer::Printer, r: Vec<Preproc>) -> Vec<Statement>
{
    let mut parser = PreprocParser{commands: r, index: 0};
    let mut ret = Vec::<Statement>::new();
    group_commands(path, print, &mut ret, &mut parser, 0);
    ret
}


fn handle_lines(print: &mut printer::Printer, args: &LinesArg) -> Result<(), Fail>
{
    let source_lines : Vec<String> = core::read_file_to_lines(&args.filename)?.map(|l| l.unwrap()).collect();
    let joined_lines = join_lines(source_lines);
    let trim_lines = joined_lines.iter().map(|str| {str.trim_start().to_string()}).collect();
    let lines = remove_cpp_comments(trim_lines);
    if args.statements || args.blocks
    {
        let statements = parse_to_statements(lines);

        if args.blocks
        {
            let blocks = parse_to_blocks(&args.filename, print, statements);
            for block in blocks
            {
                print.info(format!("{:#?}", block).as_str());
            }
        }
        else
        {
            for statement in statements
            {
                print.info(format!("{:?}", statement).as_str());
            }
        }
    }
    else
    {
        for line in lines
        {
            print.info(&line.text);
        }
    }

    Ok(())
}

struct FileWalker<FileLookup>
where
    FileLookup: Fn(&Path) -> Option<Vec::<PathBuf>>,
{
    file_lookup: FileLookup,
    stats: FileStats
}

struct FileStats
{
    includes: counter::Counter<String, usize>,
    missing: counter::Counter<String, usize>,
    file_count : usize,
    total_file_count : usize,
}

fn resolve_path(directories: &Vec::<PathBuf>, stem: &str, caller_file: &Path, use_relative_path: bool) -> Option<PathBuf>
{
    if use_relative_path
    {
        if let Some(caller) = caller_file.parent()
        {
            let r = core::join(caller, stem);
            // println!("testing relative include {}", r.display());
            if r.exists()
            {
                // println!("using local {}", r.display());
                return Some(r);
            }
        }
    }
    for d in directories
    {
        let r = core::join(d, stem);
        if r.exists()
        {
            return Some(r);
        }
    }

    None
}

fn ident_split(val: &str) -> (String, String)
{
    let re_ident = Regex::new(r"[a-zA-Z_][a-zA-Z_0-9]*").unwrap();

    match re_ident.find(val)
    {
        Some(f) =>
        {
            let key = f.as_str().to_string();
            let value = val[f.end()..].to_string();
            (key, value)
        },
        None => (val.to_string(), "".to_string())
    }
}


impl<FileLookup> FileWalker<FileLookup>
where
    FileLookup: Fn(&Path) -> Option<Vec::<PathBuf>>,
{
    fn add_include(&mut self, path: &Path)
    {
        let d = path.display().to_string();
        self.stats.includes[&d] += 1;
    }

    fn add_missing(&mut self, _: &Path, include: &str)
    {
        let is = include.to_string();
        self.stats.includes[&is] += 1;
        self.stats.missing[&is] += 1;
    }

    fn walk
    (
        &mut self,
        print: &mut printer::Printer,
        path: &Path,
        file_cache: &mut HashMap<PathBuf, Vec<Statement>>
    ) -> Result<(), Fail>
    {
        // println!("------------------------------------------------------------");
        // println!("------------------------------------------------------------");
        // println!("------------------------------------------------------------");
        // println!("------------------------------------------------------------");
        // println!("------------------------------------------------------------");
        // println!("------------------------------------------------------------");
        print.info(format!("Parsing {}", path.display()).as_str());
        self.stats.file_count += 1;

        // todo(Gustav): add local directory to directory list/differentiate between <> and "" includes
        let directories = match (self.file_lookup)(path)
        {
            Some(x) => x,
            None =>
            {
                print.error(format!("Unable to get include directories for {}", path.display()).as_str());
                return Ok(());
            }
        };

        let mut included_file_cache = HashSet::new();
        let mut defines = HashMap::<String, String>::new();

        self.walk_rec(print, &directories, &mut included_file_cache, path, &mut defines, file_cache, 0)
    }

    fn walk_rec
    (
        &mut self,
        print: &mut printer::Printer,
        directories: &Vec<PathBuf>,
        included_file_cache: &mut HashSet<String>,
        path: &Path,
        defines: &mut HashMap<String, String>,
        file_cache: &mut HashMap<PathBuf, Vec<Statement>>,
        depth: usize
    ) -> Result<(), Fail>
    {
        // print.info(format!("Rec: {} {}", depth, path.display()).as_str());

        self.stats.total_file_count += 1;

        let blocks = match file_cache.get(path)
        {
            Some(b) => b.clone(),
            None =>
            {
                let source_lines : Vec<String> = core::read_file_to_lines(path)?.map(|l| l.unwrap()).collect();
                let joined_lines = join_lines(source_lines);
                let trim_lines = joined_lines.iter().map(|str| {str.trim_start().to_string()}).collect();
                let lines = remove_cpp_comments(trim_lines);
                let statements = parse_to_statements(lines);
                let b = parse_to_blocks(path, print, statements);
                file_cache.insert(path.to_path_buf(), b.clone());
                b
            }
        };

        self.block_rec(print, directories, included_file_cache, path, defines, &blocks, file_cache, depth)
    }

    // file_cache optimization:
    // 41,04s user 0,26s system 99% cpu 41,468 total
    // to
    // 7,66s user 0,16s system 99% cpu 7,850 total

    fn block_rec
    (
        &mut self,
        print: &mut printer::Printer,
        directories: &Vec<PathBuf>,
        included_file_cache: &mut HashSet<String>,
        path: &Path,
        defines: &mut HashMap<String, String>,
        blocks: &Vec<Statement>,
        file_cache: &mut HashMap<PathBuf, Vec<Statement>>,
        depth: usize
    ) -> Result<(), Fail>
    {
        for block in blocks
        {
            // todo(Gustav): improve tree-walk eval
            match block
            {
                Statement::Block(blk) =>
                {
                    match blk.name.as_str()
                    {
                        "ifdef" | "ifndef" =>
                        {
                            let key = ident_split(&blk.condition).0;

                            let ifdef = |t,f| {f && t || (!f && !t)};

                            if blk.elifs.is_empty() == false
                            {
                                // println!("elifs are unhandled, ignoring ifdef statement");
                            }
                            else
                            {
                                // println!("Key is <{}>: {:#?}", key, defines);
                                self.block_rec
                                (
                                    print, directories, included_file_cache, path, defines,
                                    if ifdef(defines.contains_key(&key), blk.name == "ifdef")
                                    {
                                        &blk.true_block
                                    }
                                    else
                                    {
                                        &blk.false_block
                                    }, file_cache, depth
                                )?;
                            }
                        },

                        _ =>
                        {
                        }
                    }
                },
                Statement::Command(cmd) =>
                {
                    match cmd.name.as_str()
                    {
                        "pragma" =>
                        {
                            match cmd.value.as_str()
                            {
                                "once" =>
                                {
                                    let path_string = path.display().to_string();
                                    if included_file_cache.contains(&path_string)
                                    {
                                        return Ok(());
                                    }
                                    else
                                    {
                                        included_file_cache.insert(path_string);
                                    }
                                }
                                _ => {}
                            }
                        },
                        "define" =>
                        {
                            let (key, value) = ident_split(&cmd.value);
                            // println!("Defining {} to {}", key, value);
                            defines.insert(key, value.trim().to_string());
                        }
                        "undef" =>
                        {
                            if defines.remove(cmd.value.trim()) == None
                            {
                                print.error(format!("{} was not defined", cmd.value).as_str());
                            }
                        }
                        "include" =>
                        {
                            let include_name = &cmd.value.trim_matches
                            (
                                |c|
                                    c == '"' ||
                                    c == '<' ||
                                    c == '>' ||
                                    c == ' '
                            );
                            match resolve_path(&directories, include_name, path, cmd.value.trim().starts_with("\""))
                            {
                                Some(sub_file) =>
                                {
                                    self.add_include(&sub_file);
                                    self.walk_rec(print, directories, included_file_cache, &sub_file, defines, file_cache, depth+1)?;
                                }
                                None =>
                                {
                                    self.add_missing(path, include_name);
                                }
                            }
                        }
                        "error" => {}, // nop
                        _ =>
                        {
                            println!("Unhandled statement {}", cmd.name);
                        }
                    }
                }
            }
        }

        Ok(())
    }
}

fn handle_files(print: &mut printer::Printer, args: &FilesArg) -> Result<(), Fail>
{
    let commmands = match args.cc.get_argument_or_none_with_cwd()
    {
        Some(path) => compilecommands::load_compile_commands(print, &path),
        None =>
        {
            print.error("Failed to get compile commands");
            return Ok(());
        }
    };

    let stats = {
        let mut walker = FileWalker
        {
            file_lookup: |file: &Path|
            {
                match commmands.get(file)
                {
                    Some(cc) => Some(cc.get_relative_includes()),
                    None => None
                }
            },
            stats: FileStats
            {
                includes: counter::Counter::<String, usize>::new(),
                missing: counter::Counter::<String, usize>::new(),
                file_count : 0,
                total_file_count: 0
            }
        };

        let mut file_cache = HashMap::<PathBuf, Vec::<Statement>>::new();

        for file in &args.sources
        {
            if file.is_absolute()
            {
                walker.walk(print, &file, &mut file_cache)?;
            }
            else
            {
                match std::env::current_dir()
                {
                    Ok(cwd) => 
                    {
                        let mut f = cwd.to_path_buf();
                        f.push(file);
                        // print.info(format!("Converted relative path {} to {}", file.display(), f.display()).as_str());
                        walker.walk(print, &f, &mut file_cache)?;
                    },
                    Err(_) => 
                    {
                        print.error("Failed to get cwd");
                        return Ok(());
                    }
                }
            }
        }

        walker.stats
    };

    print.info(format!("Top {} includes are:", args.count).as_str());

    match std::env::current_dir()
    {
        Ok(cwd) => 
        {
            for (file, count) in stats.includes.most_common().iter().take(args.count)
            {
                let d = match PathBuf::from(file).strip_prefix(&cwd)
                {
                    Ok(d) => format!("{}", d.display()),
                    Err(_) => file.to_string()
                };
                let times = (*count as f64) / stats.file_count as f64;
                print.info(format!(" - {} {:.2}x ({}/{})", d, times, count, stats.file_count).as_str());
                // todo(Gustav): include range for each include
            }
        },
        Err(_) => 
        {
            print.error("Failed to get cwd");
        }
    }

    Ok(())
}


pub fn main(print: &mut printer::Printer, _data: &builddata::BuildData, args: &Options)
{
    match args
    {
        Options::Lines{lines} =>
        {
            if let Err(err) = handle_lines(print, lines)
            {
                print.error(format!("Failed to handle lines: {}", err).as_str());
            }
        },
        Options::Files{files} =>
        {
            if let Err(err) = handle_files(print, files)
            {
                print.error(format!("Failed to handle files: {}", err).as_str());
            }
        }
    }
}
