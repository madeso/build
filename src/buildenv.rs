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
#[derive(Clone, Serialize, Deserialize, Debug, PartialEq)]
pub enum Compiler
{
    VS2015,
    VS2017,
    VS2019,
    VS2022
}


// list of platforms
#[derive(Clone, Serialize, Deserialize, Debug, PartialEq)]
pub enum Platform
{
    AUTO,
    WIN32,
    X64
}


impl FromStr for Compiler {

    type Err = String;

    fn from_str(input: &str) -> Result<Compiler, Self::Err> {
        match input.to_ascii_lowercase().as_str() {
            "vs2015" => Ok(Compiler::VS2015),
            "vs2017" => Ok(Compiler::VS2017),
            "vs2019" => Ok(Compiler::VS2019),
            "vs2022" => Ok(Compiler::VS2022),
            // github actions installed compiler
            "windows-2016" => Ok(Compiler::VS2017),
            "windows-2019" => Ok(Compiler::VS2019),
            _        => Err("invalid compiler".to_string()),
        }
    }
}


impl FromStr for Platform {

    type Err = String;

    fn from_str(input: &str) -> Result<Platform, Self::Err> {
        match input.to_ascii_lowercase().as_str() {
            "auto"  => Ok(Platform::AUTO),
            "win32" => Ok(Platform::WIN32),
            "x64"   => Ok(Platform::X64),
            "win64" => Ok(Platform::X64),
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

    // update the build environment from an argparse namespace
    pub fn update_from_args(&mut self, printer: &mut printer::Printer, args: &EnviromentArgument)
    {
        match &args.compiler
        {
            None => {},
            Some(new_compiler) =>
            {
                match &self.compiler
                {
                    None =>  {self.compiler = Some(new_compiler.clone());},
                    Some(current_compiler) =>
                    {
                        if current_compiler != new_compiler
                        {
                            if args.force
                            {
                                printer.warning(format!("Compiler changed via argument from {:#?} to {:#?}", current_compiler, new_compiler).as_str());
                                self.compiler = Some(new_compiler.clone());
                            }
                            else
                            {
                                printer.error(format!("Compiler changed via argument from {:#?} to {:#?}", current_compiler, new_compiler).as_str());
                            }
                        }
                    }
                }
            }
        }

        match &args.platform
        {
            None => {},
            Some(new_platform) =>
            {
                match &self.platform
                {
                    None =>  {self.platform = Some(new_platform.clone());},
                    Some(current_platform) =>
                    {
                        if current_platform != new_platform
                        {
                            if args.force
                            {
                                printer.warning(format!("Platform changed via argument from {:#?} to {:#?}", current_platform, new_platform).as_str());
                                self.platform = Some(new_platform.clone());
                            }
                            else
                            {
                                printer.error(format!("Platform changed via argument from {:#?} to {:#?}", current_platform, new_platform).as_str());
                            }
                        }
                    }
                }
            }
        }
    }


    // validate the build environment
    pub fn validate(&self, printer: &mut printer::Printer) -> bool
    {
        let mut status = true;
        
        match self.compiler
        {
            None =>
            {
                printer.error("Compiler not set");
                status = false;
            },
            _ => {}
        };
        
        match self.platform
        {
            None =>
            {
                printer.error("Platform not set");
                status = false;
            },
            _ => {}
        };

        status
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
