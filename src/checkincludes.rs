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

fn classify_line(missing_files: &mut HashSet<String>, only_invalid: bool, print: &mut printer::Printer, data: &builddata::BuildData, line: &str, filename: &Path, line_num: usize) -> i32
{
    let mut index = 0;
    let replacer = get_replacer(filename.file_stem().unwrap().to_str().unwrap());

    for included_regex_group in &data.includes
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
                return index;
            }
        }
        
        index += 1;
        
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

/// Check all the includes
#[derive(StructOpt, Debug)]
pub struct Options
{
    /// Files to check
    pub files: Vec<PathBuf>,

    /// Verbose printing
    #[structopt(long)]
    pub verbose: bool,

    /// Print status at the end
    #[structopt(long)]
    pub status: bool,

    /// Print only headers that couldn't be classified
    #[structopt(long)]
    pub invalid: bool
}

// wtf rust... why isn't this included by default?
// from https://users.rust-lang.org/t/how-to-find-a-substring-starting-at-a-specific-index/8299
fn find_start_at<'a>(slice: &'a str, at: usize, pat: char) -> Option<usize>
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


fn classify_file(missing_files: &mut HashSet<String>, print: &mut printer::Printer, data: &builddata::BuildData, verbose: bool, filename: &Path, print_invalid: bool) -> bool
{
    if verbose
    {
        print.info(format!("Opening file {}", filename.display()).as_str());
    }
    
    let mut includes = Vec::<Include>::new();
    let mut last_class = -1;
    let mut print_sort = false;
    let mut first_line : Option<usize> = None;
    let mut last_line : Option<usize> = None;
    
    if let Ok(lines) = core::read_file_to_lines(filename)
    {
        let mut read_lines = Vec::<String>::new();

        {
            let mut line_num = 0;
            for op_line in lines
            {
                let line = op_line.unwrap();
                line_num += 1;
                read_lines.push(line.to_string());
                
                if line.starts_with("#include")
                {
                    if first_line.is_some() == false
                    {
                        first_line = Some(line_num);
                    }
                    last_line = Some(line_num);
                    let l = line.trim_end();
                    let line_class = classify_line(missing_files, print_invalid, print, data, l, filename, line_num);
                    if line_class < 0
                    {
                        return false;
                    }
                    includes.push(Include::new(line_class, l));
                    if last_class > line_class
                    {
                        if print_invalid == false
                        {
                            error(print, filename, line_num, format!("Include order error for {}", l).as_str());
                        }
                        print_sort = true;
                    }
                    last_class = line_class;
                    if verbose
                    {
                        print.info(format!("{} {}", line_class, l).as_str());
                    }
                }
            }
        }

        if first_line.is_some() && last_line.is_some()
        {
            for line_num in first_line.unwrap() .. last_line.unwrap()
            {
                let line = read_lines[line_num-1].trim();
                if line.len() == 0 
                {
                    // ignore empty
                }
                else
                {
                    if line.starts_with("#include")
                    {
                        let end = get_text_after_include(line);
                        if end.trim().len() > 0
                        {
                            error(print, filename, line_num, format!("Invalid text after include {}", line).as_str());
                        }
                    }
                    else
                    {
                        warning(print, filename, line_num, format!("Invalid line {}", line).as_str());
                    }
                }
            }
        }
    }

    if print_sort && print_invalid == false
    {
        includes.sort_by(include_compare);
        print.info("I think the correct order would be:");
        print.info("------------------");
        let mut current_class = includes[0].line_class;
        for i in includes
        {
            if current_class != i.line_class
            {
                print.info("");
            }
            current_class = i.line_class;
            print.info(&i.line);
        }
        print.info("---------------");
        print.info("");
        print.info("");
    }

    true
}

pub fn main(print: &mut printer::Printer, data: &builddata::BuildData, args: &Options)
{
    let verbose = args.verbose;

    let mut error_count = 0;
    let mut file_count = 0;
    let mut file_error = 0;

    let mut missing_files = HashSet::new();

    for filename in &args.files
    {
        file_count += 1;
        let stored_error = error_count;

        if classify_file(&mut missing_files, print, data, verbose, filename, args.invalid) == false
        {
            error_count += 1
        }

        if error_count != stored_error
        {
            file_error += 1
        }
    }

    if args.status
    {
        print.info(format!("Files parsed: {}",  file_count).as_str());
        print.info(format!("Files errored: {}", file_error).as_str());
        print.info(format!("Errors found: {}",  error_count).as_str());
    }
}
