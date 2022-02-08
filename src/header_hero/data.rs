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
    pub ScanDirectories: Vec<PathBuf>,
    pub IncludeDirectories: Vec<PathBuf>,
    pub PrecompiledHeader: Option<PathBuf>,
    pub Files: HashMap<PathBuf, SourceFile>,
    // pub DateTime LastScan,
}

impl Project
{
    pub fn new(input: &UserInput) -> Project
    {
        // todo(Gustav): don't clone here
        Project
        {
            // ScanDirectories: Vec::<PathBuf>::new(),
            ScanDirectories: input.project_directories.clone(),
            // IncludeDirectories: Vec::<PathBuf>::new(),
            IncludeDirectories: input.include_directories.clone(),
            // PrecompiledHeader: None,
            PrecompiledHeader: input.precompiled_header.clone(),
            Files: HashMap::<PathBuf, SourceFile>::new()
            // LastScan = DateTime.Now,
        }
    }

    pub fn Clean(&mut self)
    {
        self.Files.clear();
    }
}

///////////////////////////////////////////////////////////////////////////////////////////////////
// SourceFile

#[derive(Clone)]
pub struct SourceFile
{
    pub LocalIncludes: Vec<String>,
    pub SystemIncludes: Vec<String>,
    pub AbsoluteIncludes: Vec<PathBuf>,
    pub Lines: usize,
    pub Touched: bool,
    pub Precompiled: bool,
}

impl SourceFile
{
    pub fn new() -> SourceFile
    {
        SourceFile
        {
            LocalIncludes: Vec::<String>::new(),
            SystemIncludes: Vec::<String>::new(),
            AbsoluteIncludes: Vec::<PathBuf>::new(),
            Lines: 0,
            Touched: false,
            Precompiled: false,
        }
    }
}		

pub fn IsTranslationUnitExtension(ext: &str) -> bool
{
    match ext
    {
        "cpp" | "c" | "cc" | "cxx" | "mm" | "m"
        => true,
        _ => false
    }
}

pub fn IsTranslationUnitPath(path: &Path) -> bool
{
    if let Some(os_ext) = path.extension()
    {
        if let Some(ext) = os_ext.to_str()
        {
            IsTranslationUnitExtension(ext)
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
