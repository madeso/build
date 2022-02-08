use std::path::{Path, PathBuf};
use std::cmp::Ordering;
use std::collections::HashSet;

use structopt::StructOpt;
use regex::Regex;


use crate::
{
    core,
    builddata,
    printer,
    rust
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
        main: MainData,

        /// Print all errors per file, not just the first one
        #[structopt(long)]
        all: bool
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

#[derive(Debug, Copy, Clone)]
enum MessageType
{
    Error, Warning
}

fn print_message(message_type: MessageType, print: &mut printer::Printer, filename: &Path, line: usize, message: &str)
{
    match message_type
    {
        MessageType::Error   => error(print, filename, line, message),
        MessageType::Warning => warning(print, filename, line, message)
    }
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
    print: &mut printer::Printer,
    data: &builddata::BuildData,
    line: &str,
    filename: &Path,
    line_num: usize
) -> Option<i32>
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
                            return None;
                        }
                    }
                },
                builddata::OptionalRegex::Static(re) => {re.clone()},
                builddata::OptionalRegex::Failed(err) =>
                {
                    print.error(err.as_str());
                    return None;
                }
            };

            if re.is_match(line)
            {
                return Some(index.try_into().unwrap());
            }
        }
    }

    if missing_files.contains(line) == false
    {
        missing_files.insert(line.to_string());
        error(print, filename, line_num, format!("{} is a invalid header", line).as_str());
    }
    None
}


fn get_text_after_include_quote(line: &str) -> String
{
    if let Some(start_quote) = line.find('"')
    {
        if let Some(end_quote) = rust::find_start_at(line, start_quote+1, '"')
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
    data: &builddata::BuildData,
    filename: &Path,
    verbose: bool,
    print_include_order_error_for_include: bool,
    include_error_message: MessageType
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
            let line_class = classify_line(missing_files, print, data, l, filename, line_num)?;
            r.includes.push(Include::new(line_class, l));
            if last_class > line_class
            {
                if print_include_order_error_for_include
                {
                    print_message(include_error_message, print, filename, line_num, format!("Include order error for {}", l).as_str());
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
    first_error_only: bool
) -> bool
{
    let mut ok = true;

    if let (Some(first_line_found), Some(last_line_found)) = (f.first_line, f.last_line)
    {
        for line_num in first_line_found .. last_line_found
        {
            let print_this_error = match (ok, first_error_only)
            {
                // this is not the first error AND we only want to print the first error
                (false, true) => false,
                _ => true
            };

            let line = lines[line_num-1].trim();
            if line.is_empty()
            {
                // ignore empty
            }
            else if line.starts_with("#include")
            {
                let end_not_trimmed = get_text_after_include(line);
                let end = end_not_trimmed.trim();
                if end.is_empty() == false && end.starts_with("//") == false
                {
                    if print_this_error
                    {
                        error(print, filename, line_num, format!("Invalid text after include: {}", end).as_str());
                    }
                    ok = false;
                }
            }
            else
            {
                if print_this_error
                {
                    error(print, filename, line_num, format!("Invalid line {}", line).as_str());
                }
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
    MissingPatterns,
    ListUnfixable
    {
        print_first_error_only: bool
    },
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
    let command_is_list_unfixable = match command { Command::ListUnfixable{print_first_error_only:_} => true, _ => false };
    let command_is_check          = match command { Command::Check                                   => true, _ => false };
    let command_is_fix            = match command { Command::Fix{nop:_}                              => true, _ => false };

    let print_include_order_error_for_include = command_is_check || command_is_fix;

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
    let classified = match classify_file
    (
        &lines,
        missing_files,
        print,
        data,
        filename,
        verbose,
        print_include_order_error_for_include,
        if command_is_fix { MessageType::Warning } else { MessageType::Error }
    )
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

    if !(command_is_fix || command_is_check || command_is_list_unfixable)
    {
        // if we wan't to print the unmatched header we don't care about sorting the headers
        return true;
    }

    let print_first_error_only = match command
    {
        Command::Fix{nop:_} => true,
        Command::ListUnfixable{print_first_error_only} => *print_first_error_only,
        _ => false // don't care, shouldn't be possible
    };

    if can_fix_and_print_errors(&lines, &classified, print, filename, print_first_error_only) == false
    {
        if command_is_fix
        {
            // can't fix this file... error out
            return false;
        }
    }

    if command_is_list_unfixable
    {
        return true;
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
            &command
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
        Options::ListUnfixable{main, all} => common_main(main, print, data, &Command::ListUnfixable{print_first_error_only: *all == false}),
        Options::Check{main}              => common_main(main, print, data, &Command::Check),
        Options::MissingPatterns{main}    => common_main(main, print, data, &Command::MissingPatterns),
        Options::Fix{main, write}         => common_main(main, print, data, &Command::Fix{nop: *write==false}),
    }
}
