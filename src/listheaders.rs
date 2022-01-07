use std::path::{Path, PathBuf};
use structopt::StructOpt;
use thiserror::Error;
use std::io;
use std::fmt;

use counter;

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



fn remove_cpp_comments(lines: Vec<String>) -> Vec<String>
{
    let mut r = Vec::<String>::new();
    // todo(Gustav)
    let mut in_comment = false;
    
    for line in lines
    {
        if in_comment
        {
            if line.contains("*/")
            {
                in_comment = false
                // todo(Gustav): yield part of line
            }
            else
            {
                continue;
            }
        }
        else if line.starts_with("//")
        {
            continue;
        }
        else if line.contains("/*")
        {
            // todo(Gustav): yield part of line
            in_comment = true;
            continue;
        }
        else
        {
            r.push(line.to_string());
        }
    }

    r
}


#[derive(Debug, Clone)]
struct Preproc
{
    command: String,
    arguments: String
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

// #[derive(Debug)]
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

#[derive(Debug)]
struct Command
{
    name: String,
    value: String
}

#[derive(Debug)]
struct Block
{
    name: String,
    condition: String,
    true_block: Vec<Statement>,
    false_block: Vec<Statement>
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

fn group_commands(print: &mut printer::Printer, ret: &mut Vec<Statement>, commands: &mut PreprocParser, depth: i32)
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
                false_block: Vec::<Statement>::new()
            };
            group_commands(print, &mut group.true_block, commands, depth+1);
            if peek_name(commands) == "else"
            {
                commands.skip();
                group_commands(print, &mut group.false_block, commands, depth+1);
            }
            if peek_name(commands) == "endif"
            {
                commands.skip();
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
                print.error("Ignored unmatched endif");
            }
        }
        else
        {
            ret.push(Statement::Command(Command{
                name: command.command,
                value: command.arguments
            }));
        }
    }
}


fn parse_to_statements(lines: Vec<String>) -> Vec<Preproc>
{
    let mut r = Vec::<Preproc>::new();

    for line in lines
    {
        if line.starts_with("#")
        {
            let li = line[1..].trim_start();
            let data = li.split_once(' ');

            r.push
            (
                match data
                {
                    Some(pre) =>
                    {
                        let (command, arguments) = pre;
                        Preproc{command: command.to_string(), arguments: arguments.to_string()}
                    },
                    None => Preproc{command: li.to_string(), arguments:"".to_string()}
                }
            )
        }
    }

    r
}

fn parse_to_blocks(print: &mut printer::Printer, r: Vec<Preproc>) -> Vec<Statement>
{
    let mut parser = PreprocParser{commands: r, index: 0};
    let mut ret = Vec::<Statement>::new();
    group_commands(print, &mut ret, &mut parser, 0);
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
            let blocks = parse_to_blocks(print, statements);
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
            print.info(&line);
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

fn resolve_path(directories: &Vec::<PathBuf>, stem: &str) -> Option<PathBuf>
{
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

    fn walk(&mut self, print: &mut printer::Printer, path: &Path) -> Result<(), Fail>
    {
        self.stats.file_count += 1;

        let directories = match (self.file_lookup)(path)
        {
            Some(x) => x,
            None =>
            {
                print.error(format!("Unable to get include directories for {}", path.display()).as_str());
                return Ok(());
            }
        };

        self.walk_rec(print, &directories, path)
    }
    fn walk_rec(&mut self, print: &mut printer::Printer, directories: &Vec<PathBuf>, path: &Path) -> Result<(), Fail>
    {
        self.stats.total_file_count += 1;

        let source_lines : Vec<String> = core::read_file_to_lines(path)?.map(|l| l.unwrap()).collect();
        let joined_lines = join_lines(source_lines);
        let trim_lines = joined_lines.iter().map(|str| {str.trim_start().to_string()}).collect();
        let lines = remove_cpp_comments(trim_lines);
        let statements = parse_to_statements(lines);
        let blocks = parse_to_blocks(print, statements);
        
        for block in blocks
        {
            // print.info(format!("{:#?}", block).as_str());
            if let Statement::Command(cmd) = block
            {
                if cmd.name == "include"
                {
                    let include_name = &cmd.value.trim_matches
                    (
                        |c|
                            c == '"' ||
                            c == '<' ||
                            c == '>' ||
                            c == ' '
                    );
                    match resolve_path(&directories, include_name)
                    {
                        Some(sub_file) =>
                        {
                            self.add_include(&sub_file);
                            self.walk_rec(print, directories, &sub_file)?;
                        }
                        None =>
                        {
                            self.add_missing(path, include_name);
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

        for file in &args.sources
        {
            if file.is_absolute()
            {
                walker.walk(print, &file)?;
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
                        walker.walk(print, &f)?;
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
