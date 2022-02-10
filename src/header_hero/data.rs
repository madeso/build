///////////////////////////////////////////////////////////////////////////////////////////////////
// Project

use std::collections::HashMap;
use std::path::{Path, PathBuf};

use serde::{Serialize, Deserialize};

use crate::
{
    core
};


#[derive(Serialize, Deserialize, Debug)]
pub struct UserInput
{
    pub project_directories: Vec<PathBuf>,
    pub include_directories: Vec<PathBuf>,
    pub precompiled_header: Option<PathBuf>,
}

impl UserInput
{
    pub fn load_from_file(file: &Path) -> Result<UserInput, String>
    {
        let content = core::read_file_to_string(&file).ok_or(format!("Unable to read file: {}", file.to_string_lossy()))?;
        let data : Result<UserInput, serde_json::error::Error> = serde_json::from_str(&content);
        match data
        {
            Ok(loaded) =>
            {
                Ok(loaded)
            },
            Err(error) => Err(format!("Unable to parse json {}: {}", file.to_string_lossy(), error))
        }
    }

    pub fn decorate(&mut self, root: &Path)
    {
        for d in &mut self.project_directories
        {
            if d.exists() { continue; }

            let mut np = root.to_path_buf();
            np.push(d.clone());

            println!("{} does not exist: {}", d.display(), np.display());

            if np.exists()
            {
                *d = np;
                println!("replaced!");
            }
        }
    }
}

pub struct Project
{
    pub scan_directories: Vec<PathBuf>,
    pub include_directories: Vec<PathBuf>,
    pub precompiled_header: Option<PathBuf>,

    pub scanned_files: HashMap<PathBuf, SourceFile>,
    // pub DateTime LastScan,
}

impl Project
{
    pub fn new(input: &UserInput) -> Project
    {
        // todo(Gustav): don't clone here
        Project
        {
            scan_directories: input.project_directories.clone(),
            include_directories: input.include_directories.clone(),
            precompiled_header: input.precompiled_header.clone(),
            scanned_files: HashMap::<PathBuf, SourceFile>::new()
            // LastScan = DateTime.Now,
        }
    }

    // pub fn Clean(&mut self)
    // {
    //     self.scanned_files.clear();
    // }
}

///////////////////////////////////////////////////////////////////////////////////////////////////
// SourceFile

#[derive(Clone)]
pub struct SourceFile
{
    pub local_includes: Vec<String>,
    pub system_includes: Vec<String>,
    pub absolute_includes: Vec<PathBuf>,
    pub number_of_lines: usize,
    pub is_touched: bool,
    pub is_precompiled: bool,
}

impl SourceFile
{
    pub fn new() -> SourceFile
    {
        SourceFile
        {
            local_includes: Vec::<String>::new(),
            system_includes: Vec::<String>::new(),
            absolute_includes: Vec::<PathBuf>::new(),
            number_of_lines: 0,
            is_touched: false,
            is_precompiled: false,
        }
    }
}		

pub fn is_translation_unit_extension(ext: &str) -> bool
{
    match ext
    {
        "cpp" | "c" | "cc" | "cxx" | "mm" | "m"
        => true,
        _ => false
    }
}

pub fn is_translation_unit(path: &Path) -> bool
{
    if let Some(os_ext) = path.extension()
    {
        if let Some(ext) = os_ext.to_str()
        {
            is_translation_unit_extension(ext)
        }
        else
        {
            // extension is not a valid utf8
            false
        }
    }
    else
    {
        false
    }
}
