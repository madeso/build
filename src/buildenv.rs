use std::path::{Path, PathBuf};

use serde::{Serialize, Deserialize};

use crate::
{
    core,
    printer
};


// list of compilers
#[derive(Serialize, Deserialize, Debug)]
pub enum Compiler
{
    VS2015,
    VS2017,
    VS2019,
    VS2022
}


// list of platforms
#[derive(Serialize, Deserialize, Debug)]
pub enum Platform
{
    AUTO,
    WIN32,
    X64
}
    
#[derive(Serialize, Deserialize, Debug)]
pub struct BuildEnviroment
{
    pub compiler: Option<Compiler>,
    pub platform: Option<Platform>
}

impl BuildEnviroment
{
    pub fn new_empty() -> BuildEnviroment
    {
        BuildEnviroment
        {
            compiler: None,
            platform: None
        }
    }
}

// load build enviroment from json file
pub fn load_from_file(path: &Path, printer: Option<&mut printer::Printer>) -> BuildEnviroment
{
    match core::read_file_to_string(path)
    {
        Some(content) =>
        {
            let data : Result<BuildEnviroment, serde_json::error::Error> = serde_json::from_str(&content);
            match data
            {
                Ok(loaded) => loaded,
                Err(error) =>
                {
                    match printer
                    {
                        Some(pr) => pr.error(format!("Unable to parse json {}: {}", path.to_string_lossy(), error).as_str()),
                        None => {}
                    }
                    BuildEnviroment::new_empty()
                }
            }
        },
        None => BuildEnviroment::new_empty()
    }
}
