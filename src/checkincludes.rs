use std::path::{Path, PathBuf};
use std::cmp::Ordering;
use std::collections::HashSet;

use structopt::StructOpt;
use regex::Regex;


use crate::
{
    core,
    builddata,
    printer
};


#[derive(StructOpt, Debug)]
pub struct MainData
{
    /// Files to look at
    pub files: Vec<PathBuf>,

    /// Print general file status at the end
    #[structopt(long)]
    pub status: bool,

    /// Use verbose output
    #[structopt(long)]
    pub verbose: bool,
}


#[derive(StructOpt, Debug)]
pub enum Options
{
    /// Print headers that don't match any pattern so you can add more regexes
    MissingPatterns
    {
        #[structopt(flatten)]
        main: MainData
    },
    
    /// Print headers that can't be fixed
    ListUnfixable
    {
        #[structopt(flatten)]
        main: MainData
    },


    /// Check for style errors and error out
    Check
    {
        #[structopt(flatten)]
        main: MainData
    },

    /// Fix style errors and print unfixable
    Fix
    {
        #[structopt(flatten)]
        main: MainData,

        /// Write fixes to file
        #[structopt(long)]
        write: bool
    }
}



struct Include
{
    line_class: i32,
    line: String
}

impl Include
{
    fn new(c: i32, l: &str) -> Include
    {
        Include
        {
            line_class: c,
            line: l.to_string()
        }
    }
}


fn include_compare(lhs: &Include, rhs: &Include) -> Ordering
{
    if lhs.line_class == rhs.line_class
    {
        lhs.line.cmp(&rhs.line)
    }
    else
    {
        lhs.line_class.cmp(&rhs.line_class)
    }
}

fn error(print: &mut printer::Printer, filename: &Path, line: usize, message: &str)
{
    print.error(format!("{}({}): error CHK3030: {}", filename.display(), line, message).as_str());
}

fn warning(print: &mut printer::Printer, filename: &Path, line: usize, message: &str)
{
    print.warning(format!("{}({}): warning CHK3030: {}", filename.display(), line, message).as_str());
}

pub fn get_replacer(file_stem: &str) -> core::TextReplacer
{
    let mut replacer = core::TextReplacer::new();
    replacer.add("{file_stem}", file_stem);
    replacer
}


fn classify_line
(
    missing_files: &mut HashSet<String>,
    only_invalid: bool,
    print: &mut printer::Printer,
    data: &builddata::BuildData,
    line: &str,
    filename: &Path,
    line_num: usize
) -> i32
{
    let replacer = get_replacer(filename.file_stem().unwrap().to_str().unwrap());

    for (index, included_regex_group) in data.includes.iter().enumerate()
    {
        for included_regex in included_regex_group
        {
            let re = match included_regex
            {
                builddata::OptionalRegex::Dynamic(regex) =>
                {
                    let regex_source = replacer.replace(regex);
                    match Regex::new(&regex_source)
                    {
                        Ok(re) => { re },
                        Err(err) =>
                        {
                            print.error(format!("{} -> {} is invalid regex: {}", regex, regex_source, err).as_str());
                            return -1;
                        }
                    }
                },
                builddata::OptionalRegex::Static(re) => {re.clone()},
                builddata::OptionalRegex::Failed(err) =>
                {
                    print.error(err.as_str());
                    return -1;
                }
            };

            if re.is_match(line)
            {
                return index.try_into().unwrap();
            }
        }
    }

    if only_invalid
    {
        if missing_files.contains(line)
        {
            return -1;
        }
        missing_files.insert(line.to_string());
    }

    error(print, filename, line_num, format!("{} is a invalid header", line).as_str());
    -1
}


// wtf rust... why isn't this included by default?
// from https://users.rust-lang.org/t/how-to-find-a-substring-starting-at-a-specific-index/8299
fn find_start_at(slice: &str, at: usize, pat: char) -> Option<usize>
{
    slice[at..].find(pat).map(|i| at + i)
}


fn get_text_after_include_quote(line: &str) -> String
{
    if let Some(start_quote) = line.find('"')
    {
        if let Some(end_quote) = find_start_at(line, start_quote+1, '"')
        {
            line[end_quote+1..].to_string()
        }
        else
        {
            "".to_string()
        }
    }
    else
    {
        "".to_string()
    }
}


fn get_text_after_include(line: &str) -> String
{
    if let Some(angle) = line.find('>')
    {
        if let Some(start_quote) = line.find('"')
        {
            if start_quote < angle
            {
                return get_text_after_include_quote(line);
            }
        }

        line[angle+1..].to_string()
    }
    else
    {
        get_text_after_include_quote(line)
    }
}


struct ClassifiedFile
{
    first_line: Option<usize>,
    last_line: Option<usize>,
    includes: Vec<Include>,
    invalid_order: bool,
}


fn classify_file
(
    lines: &[String],
    missing_files: &mut HashSet<String>,
    print: &mut printer::Printer,
    print_unmatched_header: bool,
    data: &builddata::BuildData,
    filename: &Path,
    verbose: bool
) -> Option<ClassifiedFile>
{

    let mut r = ClassifiedFile
    {
        first_line: None,
        last_line: None,
        includes: Vec::<Include>::new(),
        invalid_order: false
    };

    let mut last_class = -1;
    let mut line_num = 0;
    for line in lines
    {
        line_num += 1;
        
        if line.starts_with("#include")
        {
            if r.first_line.is_some() == false
            {
                r.first_line = Some(line_num);
            }
            r.last_line = Some(line_num);
            let l = line.trim_end();
            let line_class = classify_line(missing_files, print_unmatched_header, print, data, l, filename, line_num);
            if line_class < 0
            {
                return None;
            }
            r.includes.push(Include::new(line_class, l));
            if last_class > line_class
            {
                if print_unmatched_header == false
                {
                    error(print, filename, line_num, format!("Include order error for {}", l).as_str());
                }
                r.invalid_order = true;
            }
            last_class = line_class;
            if verbose
            {
                print.info(format!("{} {}", line_class, l).as_str());
            }
        }
    }

    r.includes.sort_by(include_compare);

    Some(r)
}


fn can_fix_and_print_errors
(
    lines: &[String],
    f: &ClassifiedFile,
    print: &mut printer::Printer,
    filename: &Path,
) -> bool
{
    let mut ok = true;

    if let (Some(first_line_found), Some(last_line_found)) = (f.first_line, f.last_line)
    {
        for line_num in first_line_found .. last_line_found
        {
            let line = lines[line_num-1].trim();
            if line.is_empty()
            {
                // ignore empty
            }
            else if line.starts_with("#include")
            {
                let end = get_text_after_include(line);
                if end.trim().is_empty() == false
                {
                    error(print, filename, line_num, format!("Invalid text after include {}", line).as_str());
                    ok = false;
                }
            }
            else
            {
                warning(print, filename, line_num, format!("Invalid line {}", line).as_str());
                ok = false;
            }
        }
    }

    ok
}


fn generate_suggested_include_lines_from_sorted_includes(includes: &[Include]) -> Vec<String>
{
    let mut new_lines = Vec::<String>::new();
    let mut current_class = includes[0].line_class;
    for i in includes
    {
        if current_class != i.line_class
        {
            new_lines.push("".to_string());
        }
        current_class = i.line_class;
        
        new_lines.push(i.line.to_string());
    }

    new_lines
}


fn compose_new_file_content(first_line_found: usize, last_line_found: usize, new_lines: &[String], lines: &[String]) -> Vec::<String>
{
    let mut file_data = Vec::<String>::new();
    
    for line_num in 1 .. first_line_found
    {
        file_data.push(lines[line_num-1].to_string());
    }
    for line in new_lines
    {
        file_data.push(line.to_string());
    }
    for line_num in last_line_found+1 .. lines.len()+1
    {
        file_data.push(lines[line_num-1].to_string());
    }

    file_data
}


fn print_lines(print: &mut printer::Printer, lines: &[String])
{
    print.info("*************************************************");
    for line in lines
    {
        print.info(line);
    }
    print.info("*************************************************");
    print.info("");
    print.info("");
}


enum Command
{
    Validate,
    Check,
    Fix
    {
        nop: bool
    }
}

fn run_file
(
    missing_files: &mut HashSet<String>,
    print: &mut printer::Printer,
    data: &builddata::BuildData,
    verbose: bool,
    filename: &Path,
    command: &Command,
) -> bool
{
    if verbose
    {
        print.info(format!("Opening file {}", filename.display()).as_str());
    }
    
    let loaded_lines = core::read_file_to_lines(filename);
    if let Err(err) = loaded_lines
    {
        print.error(format!("Failed to load {}: {}", filename.display(), err).as_str());
        return false;
    }

    let lines = loaded_lines.unwrap();
    let classified = match classify_file(&lines, missing_files, print, matches!(command, Command::Validate), data, filename, verbose)
    {
        Some(c) => c,
        None =>
        {
            // file contains unclassified header
            return false;
        }
    };

    if classified.invalid_order == false
    {
        // this file is ok, don't touch it
        return true;
    }

    // if the include order is invalid, that means there needs to be a include and we know the start and end of it
    let first_line = classified.first_line.unwrap();
    let last_line = classified.last_line.unwrap();

    if matches!(command, Command::Validate)
    {
        // if we wan't to print the unmatched header we don't care about sorting the headers
        return true;
    }

    if can_fix_and_print_errors(&lines, &classified, print, filename) == false
    {
        match command
        {
            // can't fix this file... error out
            Command::Fix{nop:_} => return false,
            _ => {}
        }
    }

    let sorted_include_lines = generate_suggested_include_lines_from_sorted_includes(&classified.includes);

    if let Command::Fix{nop} = command
    {
        let file_data = compose_new_file_content(first_line, last_line, &sorted_include_lines, &lines);

        if *nop
        {
            print.info(format!("Will write the following to {}", filename.display()).as_str());
            print_lines(print, &file_data);
        }
        else
        {
            core::write_string_to_file(print, filename, &file_data.join("\n"));
        }
    }
    else
    {
        print.info("I think the correct order would be:");
        print_lines(print, &sorted_include_lines);
    }

    true
}


fn common_main
(
    args: &MainData,
    print: &mut printer::Printer,
    data: &builddata::BuildData,
    command: &Command
)
{
    let mut error_count = 0;
    let mut file_count = 0;
    let mut file_error = 0;

    let mut missing_files = HashSet::<String>::new();

    for filename in &args.files
    {
        file_count += 1;
        let stored_error = error_count;

        let ok = run_file
        (
            &mut missing_files,
            print,
            data,
            args.verbose,
            filename,
            command
        );

        if ok == false
        {
            error_count += 1;
        }

        if error_count != stored_error
        {
            file_error += 1;
        }
    }

    if args.status
    {
        print.info(format!("Files parsed: {}",  file_count).as_str());
        print.info(format!("Files errored: {}", file_error).as_str());
        print.info(format!("Errors found: {}",  error_count).as_str());
    }
}


pub fn main(print: &mut printer::Printer, data: &builddata::BuildData, args: &Options)
{
    match args
    {
        // todo(Gustav): add ListUnfixable

        Options::Check{main}           => common_main(main, print, data, &Command::Check),
        Options::MissingPatterns{main} => common_main(main, print, data, &Command::Validate),
        Options::Fix{main, write}      => common_main(main, print, data, &Command::Fix{nop: *write==false}),
        
        _ => { print.error("todo(Gustav): currently unhandled option!"); }
    }
}
