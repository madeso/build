use std::path::{Path, PathBuf};
use structopt::StructOpt;
use regex::Regex;
use std::cmp::Ordering;

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

fn error(print: &mut printer::Printer, filename: &Path, line: i32, message: &str)
{
    print.error(format!("{}({}): error CHK3030: {}", filename.display(), line, message).as_str());
}

fn classify_line(print: &mut printer::Printer, data: &builddata::BuildData, line: &str, filename: &Path, line_num: i32) -> i32
{
    let mut index = 0;
    let mut replacer = core::TextReplacer::new();
    replacer.add("{file_stem}", filename.file_stem().unwrap().to_str().unwrap());

    for regex in &data.includes
    {
        let regex_source = replacer.replace(regex);
        match Regex::new(&regex_source)
        {
            Ok(re) =>
            {
                if re.is_match(line)
                {
                    return index;
                }
                else
                {
                    index += 1;
                }
            },
            Err(err) =>
            {
                print.error(format!("{} -> {} is invalid regex: {}", regex, regex_source, err).as_str());
                return -1;
            }
        }
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

fn classify_file(print: &mut printer::Printer, data: &builddata::BuildData, verbose: bool, filename: &Path, print_invalid: bool) -> bool
{
    if verbose
    {
        print.info(format!("Opening file {}", filename.display()).as_str());
    }
    
    let mut line_num = 0;
    let mut includes = Vec::<Include>::new();
    let mut last_class = -1;
    let mut print_sort = false;

    if let Ok(lines) = core::read_file_to_lines(filename)
    {
        for op_line in lines
        {
            let line = op_line.unwrap();
            line_num += 1;
            if line.starts_with("// headerlint: disable")
            {
                break;
            }
            if line.starts_with("#include")
            {
                let l = line.trim_end();
                let line_class = classify_line(print, data, l, filename, line_num);
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

    for filename in &args.files
    {
        file_count += 1;
        let stored_error = error_count;

        if classify_file(print, data, verbose, &filename, args.invalid) == false
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
