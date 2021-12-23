use std::path::{Path, PathBuf};
use std::env;

use serde::{Serialize, Deserialize};

use crate::
{
    core
};


#[derive(Serialize, Deserialize, Debug)]
struct ProjectFile
{
    name: String
}


#[derive(Debug)]
pub struct BuildData
{
    pub name: String
}


fn load_from_dir(root: &Path) -> Result<BuildData, String>
{
    let mut file = PathBuf::new();
    file.push(root);
    file.push("project.wb.json");
    let content = core::read_file_to_string(&file).ok_or("Unable to read file")?;
    let data : Result<ProjectFile, serde_json::error::Error> = serde_json::from_str(&content);
    match data
    {
        Ok(loaded) =>
            Ok
            (
                BuildData
                {
                    name: loaded.name
                }
            ),
        Err(error) => Err(format!("Unable to parse json {}: {}", file.to_string_lossy(), error))
    }
}


pub fn load() -> Result<BuildData, String>
{
    let path = env::current_dir().ok().ok_or("unable to get current directory")?;
    load_from_dir(&path)
}
