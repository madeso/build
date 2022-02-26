extern crate string_error;

use std::path::{Path, PathBuf};
use std::collections::HashMap;
use std::fs;
use std::time::{SystemTime, Duration};

use regex::Regex;
use serde::{Serialize, Deserialize};
use structopt::StructOpt;
use thiserror::Error;
use string_error::static_err;

use crate::
{
    core,
    printer,
    compilecommands,
    html
};

#[derive(Error, Debug)]
enum Fail
{
    #[error(transparent)]
    Io(#[from] std::io::Error),

    #[error(transparent)]
    Err(#[from] Box<dyn std::error::Error>),

    #[error(transparent)]
    Serialization(#[from] serde_json::error::Error)
}

/*
#!/usr/bin/env python3

"""
clang-tidy and clang-format related tools for the euphoria project
"""

import argparse
import os
import subprocess
import re
import collections
import sys
import json
import typing
import statistics
from timeit import default_timer as timer
import compile_commands as cc


HEADER_SIZE = 65
HEADER_SPACING = 1
HEADER_START = 3

HEADER_FILES = [".h", ".hpp", ".hxx"]
SOURCE_FILES = [".cc", ".cpp", ".cxx", ".inl"]

CLANG_TIDY_WARNING_CLASS = re.compile(r"\[(\w+([-,]\w+)+)\]")


def file_exist(file: str) -> bool:
    return os.path.isfile(file)


def get_file_data(file_name, missing_file):
    if file_exist(file_name):
        with open(file_name, "r") as f:
            return json.loads(f.read())
    else:
        return missing_file


def set_file_data(file_name, data):
    with open(file_name, "w") as f:
        printer.info(json.dumps(data, sort_keys=true, indent=4), file=f)



def printer.header(project_name, header_character="-"):
    """
    print a "pretty" header to the terminal
    """
    project = " " * HEADER_SPACING + project_name + " " * HEADER_SPACING
    start = header_character*HEADER_START
    left = HEADER_SIZE - (len(project) + HEADER_START)
    right = header_character*(left) if left > 1 else ""
    printer.info(header_character * HEADER_SIZE)
    printer.info(start+project+right)
    printer.info(header_character * HEADER_SIZE)



def multisort(xs, specs):
    for key, reverse in reversed(specs):
        xs.sort(key=key, reverse=reverse)
    return xs



def clang_tidy_lines(root):
    """
    return a iterator over the the "compiled" .clang-tidy lines
    """
    with open(clang_tidy_root(root), "r") as clang_tidy_file:
        write = false
        checks = []
        for line in clang_tidy_file:
            if write:
                l = line.rstrip()
                if !l.lstrip().starts_with("//"):
                    yield l
            else:
                stripped_line = line.strip()
                if stripped_line == "":
                    pass
                elif stripped_line[0] == "#":
                    pass
                elif stripped_line == "END_CHECKS":
                    write = true
                    checks_value = ",".join(checks)
                    yield "Checks: "{}"".format(checks_value)
                else:
                    checks.push(stripped_line)


def print_clang_tidy_source(root, clang_tidy_file):
    """
    print the clang-tidy "source"
    """
    for line in clang_tidy_lines(root):
        printer.info(line, file=clang_tidy_file)


def make_clang_tidy(root):
    """
    write the .clang-tidy from the clang-tidy "source"
    """
    with open(os.path.join(root, ".clang-tidy"), "w") as clang_tidy_file:
        print_clang_tidy_source(root, clang_tidy_file)


def path_to_output_store(build_folder):
    return os.path.join(build_folder, "clang-tidy-store.json")




def get(dictionary, key):
    if dictionary.contains(key):
        return dictionary[key]
    return None



def set_existing_output(root, project_build_folder, source_file, existing_output, time):
    store = get_store(project_build_folder)
    root_file = clang_tidy_root(root)
    data = {}
    data["time"] = get_last_modification([root_file, source_file])
    data["output"] = existing_output
    data["time_took"] = time
    store[source_file] = data
    set_file_data(path_to_output_store(project_build_folder), store)



def total(counter):
    """
    returns the total number of items in a counter
    """
    return sum(counter.values())


##############################################################################
##############################################################################

def handle_list(args):
    root = os.getcwd()

    project_build_folder = cc.find_build_root(root)
    if project_build_folder == None:
        printer.info("unable to find build folder")
        return

    files = list_files_in_folder(root, SOURCE_FILES)

    if args.sort:
        sorted = sort_and_map_files(root, files)
        for project, source_files in sorted.items():
            printer.header(project)
            for source_file in source_files:
                printer.info(source_file)
            printer.info("")
    else:
        for file in files:
            printer.info(file)


def handle_format(args):
    """
    callback function called when running clang.py format
    """
    root = os.getcwd()

    project_build_folder = cc.find_build_root(root)
    if project_build_folder == None:
        printer.info("unable to find build folder")
        return

    data = extract_data_from_root(root, SOURCE_FILES + HEADER_FILES)

    for project, source_files in data.items():
        printer.header(project)
        for source_file in source_files:
            printer.info(os.path.basename(source_file), flush=true)
            if args.nop == false:
                subprocess.call(["clang-format", "-i", source_file])
        printer.info("")


def handle_make_tidy(args):
    """
    callback function called when running clang.py make
    """
    root = os.getcwd()
    if args.nop:
        print_clang_tidy_source(root, sys.stdout)
    else:
        make_clang_tidy(root)




##############################################################################


def main():
    """
    entry point function for running the clang.py script
    """
    parser = argparse.ArgumentParser(description="do clang stuff")
    sub_parsers = parser.add_subparsers(dest="command_name", title="Commands",
                                        help="", metavar="<command>")

    sub = sub_parsers.add_parser("make", help="make .clang-tidy")
    sub.add_argument("--nop", action="store_true", help="dont write anything")
    sub.set_defaults(func=handle_make_tidy)

    sub = sub_parsers.add_parser("format", help="do clang format on files")
    sub.add_argument("--nop", action="store_true", help="dont do anything")
    sub.set_defaults(func=handle_format)

    sub = sub_parsers.add_parser("ls", help="list files")
    sub.add_argument("--new", action="store_true", help="use new lister")
    sub.add_argument("--sort", action="store_true", help="sort listing")
    sub.set_defaults(func=handle_list)

    args = parser.parse_args()
    if args.command_name != None:
        args.func(args)
    else:
        parser.print_help()


##############################################################################

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
*/

#[derive(StructOpt, Debug)]
pub struct TidySharedArguments
{
    /// the clang-tidy to use
    #[structopt(long)]
    tidy: Option<PathBuf>,

    /// Force clang-tidy to run, even if there is a result
    #[structopt(long)]
    force : bool,

    /// don't tidy headers
    #[structopt(long)]
    no_headers: bool,

    /// don't do anything
    #[structopt(long)]
    nop : bool,

    #[structopt(long)]
    filter: Option<Vec<String>>
}


impl TidySharedArguments
{
    fn headers(&self) -> bool
    {
        self.no_headers == false
    }
}

#[derive(StructOpt, Debug)]
pub struct TidyConsoleArguments
{
    /// try to fix the source
    #[structopt(long)]
    fix : bool,
    
    /// use shorter and stop after one file
    #[structopt(long)]
    short : bool,

    /// also list files in the summary
    #[structopt(long)]
    list : bool,    

    #[structopt(long)]
    only: Option<Vec<String>>,

    #[structopt(flatten)]
    shared: TidySharedArguments
}


#[derive(StructOpt, Debug)]
pub struct TidyHtmlArguments
{
    /// where to dump the html
    output_folder: PathBuf,

    #[structopt(flatten)]
    shared: TidySharedArguments
}

#[derive(StructOpt, Debug)]
pub struct TidyCacheArguments
{
    input: PathBuf
}

/// Helper that simplifies various clang tools
#[derive(StructOpt, Debug)]
pub enum Options
{
    /// call clang-tidy
    Tidy
    {
        #[structopt(flatten)]
        arg: TidyConsoleArguments
    },

    /// call clang-tidy but output html
    TidyReport
    {
        #[structopt(flatten)]
        arg: TidyHtmlArguments
    },

    TidyCache
    {
        #[structopt(flatten)]
        arg: TidyCacheArguments
    }
}


///////////////////////////////////////////////////////////////////////////////////////////////////


fn clang_tidy_root(root: &Path) -> PathBuf
{
    return core::join(root, "clang-tidy")
}


fn path_to_output_store(build_folder: &Path) -> PathBuf
{
    return core::join(build_folder, "clang-tidy-store.json");
}


///////////////////////////////////////////////////////////////////////////////////////////////////



fn get_last_modified(file: &Path) -> Result<SystemTime, Fail>
{
    let metadata = fs::metadata(file)?;
    Ok(metadata.modified()?)
}


fn get_last_modification(input_files: &[&Path]) -> Option<SystemTime>
{
    let mut sourcemod = None;

    for path in input_files
    {
        match get_last_modified(path)
        {
            Ok(time) =>
            {
                match sourcemod
                {
                    Some(prev) =>
                    {
                        if time > prev
                        {
                            sourcemod = Some(time);
                        }
                    }
                    None =>
                    {
                        sourcemod = Some(time);
                    }
                }
            }
            _ =>
            {
                // ignore error
            }
        }
    }
    
    sourcemod
}



fn is_all_up_to_date(input_files: &[&Path], output: SystemTime) -> bool
{
    match get_last_modification(input_files)
    {
        Some(last_input) => 
        {
            last_input <= output
        }
        None => false
    }
}


#[derive(Serialize, Deserialize, Clone, Debug)]
struct TidyCache
{
    // the output of clang-tidy
    pub output: String,

    // time clang-tidy was run
    pub time: f32,

    // the number of seconds it took
    pub time_took: f32,
}

impl TidyCache
{
    // Use TidyCache::load_from_file_safe instead of get_file_data
    fn load_from_file(file: &Path) -> Result<HashMap<PathBuf, TidyCache>, String>
    {
        let content = core::read_file_to_string(file).ok_or(format!("Unable to read file: {}", file.to_string_lossy()))?;
        let data : Result<HashMap<PathBuf, TidyCache>, serde_json::error::Error> = serde_json::from_str(&content);
        match data
        {
            Ok(loaded) =>
            {
                Ok(loaded)
            },
            Err(error) => Err(format!("Unable to parse json {}: {}", file.to_string_lossy(), error))
        }
    }

    fn when(&self) -> SystemTime
    {
        let dur = Duration::from_secs_f32(self.time);
        let time = SystemTime::UNIX_EPOCH.checked_add(dur).unwrap();
        time
    }
}




fn get_store(build_folder: &Path) -> HashMap<PathBuf, TidyCache>
{
    let loaded = TidyCache::load_from_file(&path_to_output_store(build_folder));
    match loaded
    {
        Ok(r) => r,
        Err(err) =>
        {
            println!("{:?}", err);
            HashMap::new()
        }
    }
}



fn get_existing_output(root: &Path, project_build_folder: &Path, source_file: &Path) -> Option<TidyCache>
{
    let store = get_store(project_build_folder);

    match store.get(source_file)
    {
        Some(stored) =>
        {
            let root_file = clang_tidy_root(root);
            let time = stored.when();
            // todo(Gustav): remove when rust cache is fully implmented
            // if is_all_up_to_date(&[&root_file, source_file], time)
            // {
                Some(stored.clone())
            //}
            //else
            //{
            //    None
            //}
        },
        None => None
    }
}


struct NamePrinter
{
    name: String,
    printed: bool
}

impl NamePrinter
{
    fn new(name: &str) -> NamePrinter
    {
        NamePrinter
        {
            name: name.to_string(),
            printed: false
        }
    }

    fn print_name(&mut self, printer: &printer::Printer)
    {
        if !self.printed
        {
            printer.info(&self.name);
            self.printed = true;
        }
    }
}


struct TimingStats
{
    data: HashMap<PathBuf, Duration>
}


impl TimingStats
{
    fn new() -> TimingStats
    {
        TimingStats
        {
            data: HashMap::new()
        }
    }

    fn add(&mut self, file: &Path, time: Duration)
    {
        self.data.insert(file.to_path_buf(), time);
    }

    fn print_data(&self, printer: &printer::Printer)
    {
        if self.data.len() != 0
        {
            // let average_value = statistics.mean(self.data.values());
            let (min_name, min_value) = self.data.iter().min_by_key(|kvp| {kvp.1} ).unwrap();
            let (max_name, max_value) = self.data.iter().max_by_key(|kvp| {kvp.1} ).unwrap();
            // printer.info(f"average: {average_value:.2f}s")
            printer.info(&format!("max: {:.2}s for {}", max_value.as_secs(), max_name.display()));
            printer.info(&format!("min: {:.2}s for {}", min_value.as_secs(), min_name.display()));
            printer.info(&format!("{} files", self.data.len()));
        }
    }
}



/// runs clang-tidy and returns all the text output
fn call_clang_tidy(root: &Path, force: bool, tidy_path: &Path, project_build_folder: &Path, source_file: &Path, fix: bool) -> Option<TidyCache>
{ 
    if !force
    {
        if let Some(cache) = get_existing_output(root, project_build_folder, source_file)
        {
            return Some(cache);
        }
    }

    println!("ERROR: clang-tidy cache too old!");
    None

    /*
    command = [tidy_path, "-p", project_build_folder];
    if fix
    {
        command.push("--fix");
    }
    command.push(source_file);

    try
    {
        name_printer.print_name();
        start = timer();
        output = subprocess.check_output(command, universal_newlines=true, encoding="utf8", stderr=subprocess.STDOUT);
        end = timer();
        took = end - start;
        set_existing_output(root, project_build_folder, source_file, output, took);
        return output, took;
    }
    except subprocess.CalledProcessError as err
    {
        printer.info(err.returncode);
        if err.output != None
        {
            printer.info(err.output);
        }
        sys.exit(err.returncode);
    }*/
}


struct ClangTidyWarning
{
    classes: Vec<String>,
    lines: Vec<String>,
}

impl ClangTidyWarning
{
    fn new() -> ClangTidyWarning
    {
        ClangTidyWarning
        {
            classes: Vec::new(),
            lines: Vec::new()
        }
    }
}


fn is_clang_tidy_junk_line(line: &str) -> bool
{
    line.find("warnings generated").is_some()
    || line.find("Use -header-filter=.* to display errors").is_some()
    || (line.find("Suppressed").is_some() && line.find("NOLINT).").is_some())
    || (line.find("Suppressed").is_some() && line.find("non-user code").is_some())
}

struct ClangTidyWarningCollector
{
    current_warning : Option<ClangTidyWarning>,
    warnings: Vec<ClangTidyWarning>
}

impl ClangTidyWarningCollector
{
    fn new() -> ClangTidyWarningCollector
    {
        ClangTidyWarningCollector
        {
            current_warning: None,
            warnings: Vec::new()
        }
    }

    fn complete(&mut self)
    {
        if let Some(warning) = self.current_warning.take()
        {
            self.warnings.push(warning);
        }
    }

    fn new_warning(&mut self)
    {
        self.complete();
        self.current_warning = Some(ClangTidyWarning::new());
    }

    fn push_class(&mut self, class: &str)
    {
        if let Some(warning) = self.current_warning.as_mut()
        {
            warning.classes.push(class.to_string());
        }
        else
        {
            println!("ERROR: Attempted to add class '{}' to missing error", class);
        }
    }

    fn push_line(&mut self, line: &str)
    {
        if let Some(warning) = self.current_warning.as_mut()
        {
            warning.lines.push(line.to_string());
        }
        else
        {
            println!("ERROR: Attempted to add line to missing error");
        }
    }
}

fn extract_warnings_from_clang_tidy_output(output: &str) -> Vec<ClangTidyWarning>
{
    let CLANG_TIDY_WARNING_CLASS = Regex::new("\\[(\\w+([-,]\\w+)+)\\]").unwrap();

    let mut collector = ClangTidyWarningCollector::new();
    let mut print_empty = false;

    for line in output.lines()
    {
        if is_clang_tidy_junk_line(line) { }
        else
        {
            if line.find("warning: ").is_some()
            {
                collector.new_warning();

                if let Some(tidy_class) =  CLANG_TIDY_WARNING_CLASS.captures(line)
                {
                    let warning_classes = tidy_class.get(1).unwrap().as_str();
                    for warning_class in warning_classes.split(",")
                    {
                        collector.push_class(warning_class);
                    }
                }
            }

            if line.trim() == ""
            {
                if print_empty
                {
                    collector.push_line("");
                    print_empty = false;
                }
            }
            else
            {
                print_empty = true;
                collector.push_line(line);
            }
        }
    }

    collector.complete();
    collector.warnings
}


struct ClangTidyRun
{
    time_took: f32,
    warnings: Vec::<ClangTidyWarning>,
}


struct ClangTidyStats
{
    warnings: counter::Counter<String, usize>,
    classes: counter::Counter<String, usize>
}


/// runs the clang-tidy process, printing status to terminal
fn run_clang_tidy
(
    root: &Path,
    force: bool,
    tidy_path: &Path,
    source_file: &Path,
    project_build_folder: &Path,
    fix: bool
) -> Option<ClangTidyRun>
{
    // output
    let cache = call_clang_tidy(root, force, tidy_path, project_build_folder, source_file, fix)?;
    
    let warnings = extract_warnings_from_clang_tidy_output(&cache.output);
    
    Some
    (
        ClangTidyRun
        {
            time_took: cache.time_took,
            warnings
        }
    )
}


fn update_clang_tidy_stats(printable_file: &Path, run: &ClangTidyRun, stats: &mut TimingStats) -> ClangTidyStats
{
    let mut warnings = counter::Counter::<String, usize>::new();
    let mut classes = counter::Counter::<String, usize>::new();

    stats.add(printable_file, Duration::from_secs_f32(run.time_took));

    for warning in &run.warnings
    {
        warnings.update([printable_file.to_str().unwrap().to_string()]);
        
        
        for warning_class in &warning.classes
        {
            classes.update([warning_class.to_string()]);
        }
    }

    ClangTidyStats
    {
        warnings,
        classes
    }
}


fn print_clang_tidy_run
(
    run: &ClangTidyRun,
    stats: &ClangTidyStats,
    printer: &mut printer::Printer,
    short: bool,
    name_printer: &mut NamePrinter,
    printable_file: &Path,
    only: &[String]
)
{
    if !short && only.len() == 0
    {
        name_printer.print_name(printer);
        printer.info(&format!("took {:.2}s", run.time_took));
    }
    
    for warning in &run.warnings
    {
        let hidden = if only.len() > 0
        {
            let mut hide = true;

            for warning_class in &warning.classes
            {
                if only.contains(&warning_class)
                {
                    hide = false;
                }
            }

            hide
        }
        else
        {
            false
        };

        if !hidden
        {
            for line in &warning.lines
            {
                printer.info(line);
            }
        }
    }

    if !short && only.len() == 0
    {
        print_warning_counter(printer, &stats.classes, &printable_file.display().to_string());
        printer.info("");
    }
}


/// returns the total number of items in a counter
fn total(counter: &counter::Counter<String, usize>) -> usize
{
    counter.values().sum()
}


/// print warning counter to the console
fn print_warning_counter(printer: &printer::Printer, project_counter: &counter::Counter<String, usize>, project: &str)
{
    printer.info(&format!("{} warnings in {}.", total(project_counter), project));
    for (file, count) in project_counter.most_common().iter().take(10)
    {
        printer.info(&format!("{} at {}", file, count));
    }
}

fn get_file_with_same_extension(p: &Path) -> PathBuf
{
    let mut r = p.to_path_buf();
    r.set_extension("dummy");
    r
}


fn is_file_ignored(path: &Path) -> bool
{
    core::read_file_to_lines(path)
        .unwrap()
        [0]
        .starts_with("// clang-tidy: ignore")
}


fn sort_and_map_files(root: &Path, iterator_files: &Vec<PathBuf>) -> HashMap::<String, Vec<PathBuf>>
{
    let mut ret : HashMap::<String, Vec<PathBuf>> = HashMap::new();

    // get_filename = lambda x: os.path.splitext(x)[0]
    // get_ext = lambda x: os.path.splitext(x)[1]

    let mut files = iterator_files.clone();
    files.sort_by_key( |f| core::file_get_extension(f) );
    files.reverse();
    files.sort_by_key( |f| get_file_with_same_extension(&f) );

    for file in &files
    {
        let rel = file.strip_prefix(root).unwrap();
        let reld = rel.to_str().unwrap();
        if reld.starts_with("external") || reld.starts_with("build")
        {
            // ignore external folder
            // ignore build folder
        }
        else if !is_file_ignored(file)
        {
            let cat = rel.parent().unwrap().to_str().unwrap();

            add_to_hashpmap_vec(&mut ret, cat.to_string(), file.clone());
        }
    }
    
    ret
}


/// extenesions = None=all, or only some [txt, doc]
fn list_files_in_folder(path: &Path, extensions: Option<&[String]>) -> Vec<PathBuf>
{
    let paths = core::walk_files(path).unwrap();

    let mut r = Vec::new();

    for file in paths
    {
        let this_ext = core::file_get_extension(&file);
        if
            if let Some(search_exts) = extensions
            {
                search_exts.contains(&this_ext)
            }
            else
            {
                true
            }
        {
            r.push(file.to_path_buf());
        }
    }
        
    r
}


fn extract_data_from_root(root: &Path, extensions: Option<&[String]>) -> HashMap::<String, Vec<PathBuf>>
{
    let file_list = list_files_in_folder(root, extensions);
    sort_and_map_files(root, &file_list)
}

fn filter_out_file(arg_filters: &Option<Vec<String>>, file: &Path) -> bool
{
    if let Some(filters) = arg_filters
    {
        let file_text = file.to_str().unwrap();
        for f in filters.iter()
        {
            if file_text.find(f).is_some()
            {
                return false
            }
        }

        true
    }
    else
    {
        false
    }
}



///////////////////////////////////////////////////////////////////////////////////////////////////


fn make_clang_tidy(root_: &Path)
{
    // todo(Gustav): port me
}



fn get_header_files() -> Vec<String>
{
    vec!
    (
        "h".to_string(),
        "hpp".to_string(),
        "hxx".to_string()
    )
}

fn get_source_files() -> Vec<String>
{
    vec!
    (
        "cc".to_string(),
        "cpp".to_string(),
        "cxx".to_string(),
        "inl".to_string()
    )
}


/// callback function called when running clang.py tidy
fn handle_tidy_console(print: &mut printer::Printer, args: &TidyConsoleArguments)
{
    let default_only = vec!();
    let args_only = args.only.as_ref().unwrap_or(&default_only);

    let root = std::env::current_dir().unwrap().to_path_buf();

    let mut runner = CommandlineTidyRunner::new
    (
        args.short,
        &root,
        args_only.to_vec()
    );

    if let Err(err) = handle_tidy_or
    (
        print,
        &mut runner,
        &root,
        &args.shared.tidy,
        args.shared.force,
        args.shared.headers(),
        args.shared.nop,
        &args.shared.filter
    )
    {
        println!("Error: {}", err);
    }
}

fn handle_tidy_report(print: &mut printer::Printer, args: &TidyHtmlArguments)
{
    let root = std::env::current_dir().unwrap().to_path_buf();

    let mut runner = HtmlTidyRunner::new(&root);

    runner.begin();

    if let Err(err) = handle_tidy_or
    (
        print,
        &mut runner,
        &root,
        &args.shared.tidy,
        args.shared.force,
        args.shared.headers(),
        args.shared.nop,
        &args.shared.filter
    )
    {
        println!("Error: {}", err);
    }
    else
    {
        if fs::create_dir_all(&args.output_folder).is_err()
        {
            print.error(&format!("Failed to generate directories: {}", &args.output_folder.display()));
        }
        else
        {
            runner.end();
            runner.write(&args.output_folder);
        }
    }
}


fn counter_extend
(
    dst: &mut counter::Counter::<String, usize>,
    src: &counter::Counter::<String, usize>
)
{
    for (key, value) in src.iter()
    {
        dst[key] += value;
    }
}


fn add_to_hashpmap_vec
<
    C:
        std::hash::Hash +
        std::cmp::Eq,
    V
>
(
    ret: &mut HashMap<C, Vec::<V>>,
    key: C,
    value: V
)
{
    if let Some(m) = ret.get_mut(&key)
    {
        m.push(value);
    }
    else
    {
        ret.insert(key, vec!(value));
    }
}


trait ClangTidyRunner
{
    fn using_clang_tidy
    (
        &mut self, print: &mut printer::Printer,
        tidy_path: &Path
    );

    fn begin_project
    (
        &mut self, print: &mut printer::Printer,
        project_name: &str
    );

    fn failed_to_run
    (
        &mut self, print: &mut printer::Printer,
        source_file: &Path
    );

    fn print_result
    (
        &mut self, print: &mut printer::Printer,
        source_file: &Path,
        run: &ClangTidyRun,
        stats: &ClangTidyStats
    );

    fn should_abort_run
    (
        &mut self, print: &mut printer::Printer,
        stats: &ClangTidyStats
    ) -> bool;

    fn print_nop
    (
        &mut self, print: &mut printer::Printer,
        source_file: &Path
    );

    fn project_end
    (
        &mut self, print: &mut printer::Printer,
        project_counter: &counter::Counter<String, usize>,
        project: &str,
        first_file: bool
    );

    fn add_result
    (
        &mut self, print: &mut printer::Printer,
        total_counter: &counter::Counter<String, usize>,
        total_classes: &counter::Counter<String, usize>,
        warnings_per_file: &HashMap::<String, Vec<&Path>>,
        stats: &TimingStats
    );
}

///////////////////////////////////////////////////////////////////////////////////////////////////
// Commandline runner

struct CommandlineTidyRunner
{
    args_short: bool,
    root: PathBuf,
    args_only: Vec<String>,
}

impl CommandlineTidyRunner
{
    fn new
    (
        args_short: bool,
        root: &Path,
        args_only: Vec<String>,
    ) -> CommandlineTidyRunner
    {
        CommandlineTidyRunner
        {
            args_short,
            root: root.to_path_buf(),
            args_only,
        }
    }
}

impl ClangTidyRunner for CommandlineTidyRunner
{
    fn using_clang_tidy
    (
        &mut self, print: &mut printer::Printer,
        tidy_path: &Path
    )
    {
        print.info(&format!("using clang-tidy: {}", tidy_path.display()));
    }

    fn begin_project
    (
        &mut self, print: &mut printer::Printer,
        project_name: &str
    )
    {
        if !self.args_short
        {
            print.header(project_name);
        }
    }

    fn failed_to_run(&mut self, print: &mut printer::Printer, source_file: &Path)
    {
        print.error(&format!("Failed to run {}", source_file.display()))
    }

    fn print_result
    (
        &mut self, print: &mut printer::Printer,
        source_file: &Path,
        run: &ClangTidyRun,
        stats: &ClangTidyStats
    )
    {
        let printable_file = source_file.strip_prefix(&self.root).unwrap();

        let mut print_name = NamePrinter::new(printable_file.to_str().unwrap());
        print_clang_tidy_run(&run, &stats, print, self.args_short, &mut print_name, &source_file, &self.args_only);
    }

    fn should_abort_run
    (
        &mut self, print: &mut printer::Printer,
        stats: &ClangTidyStats
    ) -> bool
    {
        self.args_short && stats.warnings.len() > 0
    }

    fn print_nop
    (
        &mut self, print: &mut printer::Printer,
        source_file: &Path
    )
    {
        let printable_file = source_file.strip_prefix(&self.root).unwrap();
        let mut print_name = NamePrinter::new(printable_file.to_str().unwrap());
        print_name.print_name(print);
    }

    fn project_end
    (
        &mut self, print: &mut printer::Printer,
        project_counter: &counter::Counter<String, usize>,
        project: &str,
        first_file: bool
    )
    {
        if !first_file && !self.args_short
        {
            if self.args_only.len() == 0
            {
                print_warning_counter(print, &project_counter, project);
                print.info("");
                print.info("");
            }
        }
    }

    fn add_result
    (
        &mut self, print: &mut printer::Printer,
        total_counter: &counter::Counter<String, usize>,
        total_classes: &counter::Counter<String, usize>,
        warnings_per_file: &HashMap::<String, Vec<&Path>>,
        stats: &TimingStats
    )
    {
        if !self.args_short && self.args_only.len() == 0
        {
            print.header("TIDY REPORT");
            print_warning_counter(print, &total_counter, "total");
            print.info("");
            print_warning_counter(print, &total_classes, "classes");
            print.info("");
            print.line();
            print.info("");
            for (k,v) in warnings_per_file.iter()
            {
                print.info(&format!("{}:", k));
                for f in v
                {
                    print.info(&format!("  {}", f.display()));
                }
                print.info("");
            }

            print.line();
            print.info("");
            stats.print_data(print);
        }

        if total_counter.len() > 0
        {
            print.error(&format!("Found {} errors.", total_counter.len()));
        }
    }
}
///////////////////////////////////////////////////////////////////////////////////////////////////
// Commandline runner

struct HtmlTidyRunner
{
    root: PathBuf,
    sb: String
}

impl HtmlTidyRunner
{
    fn new
    (
        root: &Path
    ) -> HtmlTidyRunner
    {
        HtmlTidyRunner
        {
            root: root.to_path_buf(),
            sb: String::new()
        }
    }

    fn begin(&mut self)
    {
        html::begin_nojoin(&mut self.sb, "clang-tidy report");
    }

    fn end(&mut self)
    {
        html::end(&mut self.sb)
    }

    fn write(&self, root: &Path)
    {
        html::write_css_file(root);
        core::write_string_to_file_or(&core::join(root, "index.html"), &self.sb).unwrap();
    }

    fn print_warning_counter(&mut self, project_counter: &counter::Counter<String, usize>, project: &str)
    {
        if project_counter.len() <= 1
        {
            return;
        }

        self.sb.push_str(&format!("<h2>Sum for {}</h2>.\n", project));

        self.sb.push_str("<div class=\"tidy-sum\">\n");
        self.sb.push_str(&format!("<span class=\"num\">{}</span> warnings in <span class=\"num\">{}</span>.\n", total(project_counter), project));
        self.sb.push_str("<table>\n");
        self.sb.push_str("<tr><th class=\"file\">File</th><th>Count</th></tr>\n");
        for (file, count) in project_counter.most_common().iter().take(10)
        {
            self.sb.push_str("<tr>\n");
            self.sb.push_str(&format!("<td class=\"file\">{}</td><td class=\"num\">{}</td>\n", file, count));
            self.sb.push_str("</tr>\n");
        }
        self.sb.push_str("</table>\n");
        self.sb.push_str("</div>\n");
    }
}

fn html_markup_tidy(line: &str) -> String
{
    // markups ^~
    // todo(Gustav): use lazy eval...
    let err_mark = Regex::new("(?P<e>(\\^~*)|(~+))").unwrap();
    err_mark.replace_all(line, "<span class=\"tidy-markup-error\">$e</span>").to_string()
}

impl ClangTidyRunner for HtmlTidyRunner
{
    fn using_clang_tidy
    (
        &mut self, print: &mut printer::Printer,
        tidy_path: &Path
    )
    {
        // print.info(&format!("using clang-tidy: {}", tidy_path.display()));
    }

    fn begin_project
    (
        &mut self, print: &mut printer::Printer,
        project_name: &str
    )
    {
        self.sb.push_str(&format!("<h2>{}</h2>\n", project_name));
    }

    fn failed_to_run(&mut self, print: &mut printer::Printer, source_file: &Path)
    {
        self.sb.push_str(&format!("<div class=\"tidy-failed-to-run\">Failed to run <span class=\"num\">{}</span></div>\n", source_file.display()));
    }

    fn print_result
    (
        &mut self, print: &mut printer::Printer,
        source_file: &Path,
        run: &ClangTidyRun,
        stats: &ClangTidyStats
    )
    {
        if run.warnings.len() == 0
        {
            return;
        }

        let printable_file = source_file.strip_prefix(&self.root).unwrap();
        
        // print_clang_tidy_run(&run, &stats, print, self.args_short, &mut print_name, &source_file, &self.args_only);

        self.sb.push_str(&format!("<h3>{}</h3>\n", printable_file.display()));

        
        self.sb.push_str("<div class=\"tidy-warnings\">\n");
        for warning in &run.warnings
        {
            self.sb.push_str("<div class=\"code\">");
            for line in &warning.lines
            {
                self.sb.push_str(&format!("{}\n", html_markup_tidy(line)));
            }
            self.sb.push_str("</div>\n");
        }
        self.sb.push_str("</div>\n");
    }

    fn should_abort_run
    (
        &mut self, print: &mut printer::Printer,
        stats: &ClangTidyStats
    ) -> bool
    {
        false
    }

    fn print_nop
    (
        &mut self, print: &mut printer::Printer,
        source_file: &Path
    )
    {
    }

    fn project_end
    (
        &mut self, print: &mut printer::Printer,
        project_counter: &counter::Counter<String, usize>,
        project: &str,
        first_file: bool
    )
    {
        self.print_warning_counter(&project_counter, project);
    }

    fn add_result
    (
        &mut self, print: &mut printer::Printer,
        total_counter: &counter::Counter<String, usize>,
        total_classes: &counter::Counter<String, usize>,
        warnings_per_file: &HashMap::<String, Vec<&Path>>,
        stats: &TimingStats
    )
    {
        self.sb.push_str("<h2>Tidy Report</h2>\n");
        self.print_warning_counter(&total_counter, "total");
        self.print_warning_counter(&total_classes, "classes");
        
        
        self.sb.push_str("<h2>Warnings</h2>\n");
        for (k,v) in warnings_per_file.iter()
        {
            self.sb.push_str(&format!("<h3>{}</h3>\n", k));
            self.sb.push_str("<ul>");
            for f in v
            {
                self.sb.push_str(&format!("<li>{}</li>\n", f.display()));
            }
            self.sb.push_str("</ul>\n")
        }
        
        // should we print timing stats or not in html report?
        stats.print_data(print);

        if total_counter.len() > 0
        {
            self.sb.push_str(&format!("<p>Found <b>{}</b> errors.</p>\n", total_counter.len()));
        }
    }
}


///////////////////////////////////////////////////////////////////////////////////////////////////
// Caller

fn handle_tidy_or
(
    printer: &mut printer::Printer,
    runner: &mut dyn ClangTidyRunner,
    root: &Path,
    args_tidy: &Option<PathBuf>,
    args_force: bool,
    args_headers : bool,
    args_nop: bool,
    args_filter: &Option<Vec<String>>
) -> Result<(), Fail>
{
    let project_build_folder = compilecommands::find_build_root(&root).ok_or(static_err("Unable to find build root"))?;

    make_clang_tidy(&root);

    let default_tidy = PathBuf::from("clang-tidy");
    let tidy_path = args_tidy.as_ref().or(Some(&default_tidy)).unwrap();
    let force = args_force;
    runner.using_clang_tidy(printer, tidy_path);

    let mut total_counter = counter::Counter::<String, usize>::new();
    let mut total_classes = counter::Counter::<String, usize>::new();
    let mut warnings_per_file = HashMap::new();

    let data = if args_headers
    {
        let mut files = get_source_files();
        files.append(&mut get_header_files());
        extract_data_from_root(&root, Some(&files))
    }
    else
    {
        extract_data_from_root(&root, Some(&get_source_files()))
    };

    let mut timing_stats = TimingStats::new();

    for (project, source_files) in &data
    {
        let mut first_file = true;
        let mut project_counter = counter::Counter::<String, usize>::new();
        for source_file in source_files
        {
            let printable_file = source_file.strip_prefix(&root).unwrap();
            if filter_out_file(&args_filter, source_file)
            {
                continue;
            }
            
            if first_file
            {
                runner.begin_project(printer, project);
                first_file = false;
            }
            if args_nop == false
            {
                let run = if let Some(r) = run_clang_tidy
                    (
                        &root,
                        false,
                        &PathBuf::new(),
                        &fs::canonicalize(&source_file)?,
                        &project_build_folder,
                        false
                    )
                {
                    r
                }
                else
                {
                    runner.failed_to_run(printer, source_file);
                    continue;
                };
                let stats = update_clang_tidy_stats(&printable_file, &run, &mut timing_stats);
                runner.print_result(printer, &source_file, &run, &stats);
                
                if runner.should_abort_run(printer, &stats)
                {
                    break;
                }
                
                counter_extend(&mut project_counter, &stats.warnings);
                counter_extend(&mut total_counter, &stats.warnings);
                counter_extend(&mut total_classes, &stats.classes);
                for k in stats.classes.keys()
                {
                    add_to_hashpmap_vec(&mut warnings_per_file, k.clone(), printable_file);
                }
            }
            else
            {
                runner.print_nop(printer, printable_file);
            }
        }

        runner.project_end(printer, &project_counter, project, first_file);
    }

    runner.add_result(printer, &total_counter, &total_classes, &warnings_per_file, &timing_stats);

    Ok(())
}


//-----------------------------------------------------------------------------


fn handle_tidy_cache(print: &mut printer::Printer, args: &TidyCacheArguments)
{
    if let Err(err) = handle_tidy_cache_or(print, args)
    {
        println!("Error: {}", err);
    }
}

fn handle_tidy_cache_or(print: &mut printer::Printer, args: &TidyCacheArguments) -> Result<(), Fail>
{
    let oroot = std::env::current_dir()?;
    let root = oroot.to_path_buf();
    let project_build_folder = compilecommands::find_build_root(&root).ok_or(static_err("Unable to find build root"))?;

    if let Some(cache) = get_existing_output(&root, &project_build_folder, &fs::canonicalize(&args.input)?)
    {
        println!("Took: {:.2}s", cache.time_took);
        println!("Ran: {}", core::display_time(cache.when()));
        println!("Output:");
        println!("{}", cache.output);
    }
    else
    {
        println!("Found none");
    }
    Ok(())
}


//-----------------------------------------------------------------------------


pub fn main(print: &mut printer::Printer, args: &Options)
{
    match args
    {
        Options::Tidy{arg} =>
        {
            handle_tidy_console(print, arg);
        },

        Options::TidyReport{arg} =>
        {
            handle_tidy_report(print, arg);
        }

        Options::TidyCache{arg} =>
        {
            handle_tidy_cache(print, arg);
        }
    }
}
