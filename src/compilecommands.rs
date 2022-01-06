use std::path::{Path, PathBuf};

use serde::{Serialize, Deserialize};
use structopt::StructOpt;
use std::collections::HashMap;
use thiserror::Error;

use crate::
{
    core,
    printer
};

const COMPILE_COMMANDS_FILE_NAME : &str = "compile_commands.json";

#[derive(Debug)]
pub struct CompileCommand
{
    directory: PathBuf,
    command: String
}


#[derive(Error, Debug)]
enum CompileCommandsError
{
    #[error(transparent)]
    Io(#[from] std::io::Error),

    #[error(transparent)]
    Serialization(#[from] serde_json::error::Error)
}


impl CompileCommand
{
    pub fn get_relative_includes(&self) -> Vec<String>
    {
        // shitty comamndline parser... beware
        let mut r = Vec::<String>::new();
        let commands = self.command.split(" ");
        for c in commands
        {
            if c.starts_with("-I")
            {
                let path = &c[2..];
                // todo(Gustav): convert path to PathBuf
                r.push(path.to_string());
            }
        }

        r
    }
}

#[derive(Serialize, Deserialize, Debug)]
struct CompileCommandJson
{
    file: String,
    directory: PathBuf,
    command: String
}


pub fn load_compile_commands(print: &mut printer::Printer, path: &Path) -> HashMap<String, CompileCommand>
{
    match load_compile_commands_or(path)
    {
        Ok(r) => r,
        Err(e) =>
        {
            print.error(format!("Unable to load compile commands from {}: {}", path.display(), e).as_str());
            HashMap::new()
        }
    }
}

fn load_compile_commands_or(path: &Path) -> Result<HashMap<String, CompileCommand>, CompileCommandsError>
{
    let content = core::read_file_to_string_x(path)?;
    let data : Result<Vec::<CompileCommandJson>, serde_json::error::Error> = serde_json::from_str(&content);
    let store = data?;

    let mut r = HashMap::new();
    for entry in store
    {
        r.insert
        (
            entry.file.to_string(),
            CompileCommand
            {
                directory: entry.directory,
                command: entry.command
            }
        );
    }
    Ok(r)
}


// find the build folder containing the compile_commands file or None
fn find_build_root(root: &Path) -> Option<PathBuf>
{
    for relative_build in ["build", "build/debug-clang"]
    {
        let build = core::join(root, relative_build);
        let compile_commands_json = core::join(&build, COMPILE_COMMANDS_FILE_NAME);
        if compile_commands_json.exists()
        {
            return Some(build);
        }
    }

    return None
}


#[derive(StructOpt, Debug)]
pub struct CompileCommandArg
{
    /// the path to compile commands
    compile_commands: Option<PathBuf>
}


impl CompileCommandArg
{
    pub fn get_argument_or_none_with_cwd(&self) -> Option<PathBuf>
    {
        match std::env::current_dir()
        {
            Ok(cwd) => self.get_argument_or_none(&cwd),
            Err(_) => None
        }
    }

    pub fn get_argument_or_none(&self, cwd: &Path) -> Option<PathBuf>
    {
        match &self.compile_commands
        {
            Some(existing) => Some(existing.to_path_buf()),
            None=>
            {
                let mut r = find_build_root(cwd)?;
                r.push(COMPILE_COMMANDS_FILE_NAME);
                Some(r)
            }
        }
    }
}





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

/// Tool to list headers
#[derive(StructOpt, Debug)]
pub enum Options
{
    /// list all files in the compile commands struct
    Files
    {
        #[structopt(flatten)]
        cc: CompileCommandArg
    },

    /// list include directories per file
    Includes
    {
        #[structopt(flatten)]
        cc: CompileCommandArg
    }
}

fn handle_files(print: &mut printer::Printer, cc: &CompileCommandArg)
{
    if let Some(path) = cc.get_argument_or_none_with_cwd()
    {
        let commands = load_compile_commands(print, &path);
        print.info(format!("{:#?}", commands).as_str());
    }
}

fn handle_includes(print: &mut printer::Printer, cc: &CompileCommandArg)
{
    if let Some(path) = cc.get_argument_or_none_with_cwd()
    {
        let commands = load_compile_commands(print, &path);
        for (file, command) in &commands
        {

            print.info(file);
            let dirs = command.get_relative_includes();
            print.info(format!("{:#?}", dirs).as_str());
        }
    }
}

pub fn main(print: &mut printer::Printer, args: &Options)
{
    match args
    {
        Options::Files{cc} =>
        {
            handle_files(print, cc);
        },
        Options::Includes{cc} =>
        {
            handle_includes(print, cc);
        }
    }
}
