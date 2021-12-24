use std::path::Path;
use std::str::FromStr;

use serde::{Serialize, Deserialize};
use structopt::StructOpt;


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


impl FromStr for Compiler {

    type Err = String;

    fn from_str(input: &str) -> Result<Compiler, Self::Err> {
        match input {
            "VS2015" => Ok(Compiler::VS2015),
            "VS2017" => Ok(Compiler::VS2017),
            "VS2019" => Ok(Compiler::VS2019),
            "VS2022" => Ok(Compiler::VS2022),
            _        => Err("invalid compiler".to_string()),
        }
    }
}


impl FromStr for Platform {

    type Err = String;

    fn from_str(input: &str) -> Result<Platform, Self::Err> {
        match input {
            "AUTO"  => Ok(Platform::AUTO),
            "WIN32" => Ok(Platform::WIN32),
            "X64"   => Ok(Platform::X64),
            _       => Err("Invalid platform".to_string()),
        }
    }
}



#[derive(Serialize, Deserialize, Debug)]
pub struct BuildEnviroment
{
    pub compiler: Option<Compiler>,
    pub platform: Option<Platform>
}

#[derive(StructOpt, Debug)]
pub struct EnviromentArgument
{
    #[structopt(long)]
    pub compiler: Option<Compiler>,

    #[structopt(long)]
    pub platform: Option<Platform>,

    #[structopt(long)]
    pub force: bool
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
