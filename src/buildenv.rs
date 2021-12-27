use std::path::Path;
use std::str::FromStr;
use std::fs;

use serde::{Serialize, Deserialize};
use structopt::StructOpt;


use crate::
{
    core,
    cmake,
    printer
};


// list of compilers
#[derive(Clone, Serialize, Deserialize, Debug, PartialEq)]
pub enum Compiler
{
    VisualStudio2015,
    VisualStudio2017,
    VisualStudio2019,
    VisualStudio2022
}


// list of platforms
#[derive(Clone, Serialize, Deserialize, Debug, PartialEq)]
pub enum Platform
{
    Auto,
    Win32,
    X64
}


impl FromStr for Compiler {

    type Err = String;

    fn from_str(input: &str) -> Result<Compiler, Self::Err> {
        match input.to_ascii_lowercase().as_str() {
            "vs2015" => Ok(Compiler::VisualStudio2015),
            "vs2017" => Ok(Compiler::VisualStudio2017),
            "vs2019" => Ok(Compiler::VisualStudio2019),
            "vs2022" => Ok(Compiler::VisualStudio2022),
            // github actions installed compiler
            "windows-2016" => Ok(Compiler::VisualStudio2017),
            "windows-2019" => Ok(Compiler::VisualStudio2019),
            _        => Err("invalid compiler".to_string()),
        }
    }
}


impl FromStr for Platform {

    type Err = String;

    fn from_str(input: &str) -> Result<Platform, Self::Err> {
        match input.to_ascii_lowercase().as_str() {
            "auto"  => Ok(Platform::Auto),
            "win32" => Ok(Platform::Win32),
            "x64"   => Ok(Platform::X64),
            "win64" => Ok(Platform::X64),
            _       => Err("Invalid platform".to_string()),
        }
    }
}


fn is_64bit(platform: &Platform) -> bool
{
    match platform
    {
        Platform::Auto => {core::is_64bit()},
        Platform::Win32 => {false},
        Platform::X64 => {true}
    }
}


fn create_cmake_arch(platform: &Platform) -> &'static str
{
    if is_64bit(platform)
    {
        "x64"
    }
    else
    {
        "Win32"
    }
}

// gets the visual studio cmake generator argument for the compiler and platform
fn create_cmake_generator(compiler: &Compiler, platform: &Platform) -> cmake::Generator
{
    match compiler
    {
        Compiler::VisualStudio2015 =>
        {
            if is_64bit(platform) { cmake::Generator::new("Visual Studio 14 2015 Win64") }
            else { cmake::Generator::new("Visual Studio 14 2015") }
        },
        Compiler::VisualStudio2017 =>
        {
            if is_64bit(platform) { cmake::Generator::new("Visual Studio 15 Win64") }
            else { cmake::Generator::new("Visual Studio 15") }
        },
        Compiler::VisualStudio2019 => { cmake::Generator::new_with_arch("Visual Studio 16 2019", create_cmake_arch(platform))},
        Compiler::VisualStudio2022 => { cmake::Generator::new_with_arch("Visual Studio 17 2022", create_cmake_arch(platform))}
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

    pub fn get_cmake_generator(&self) -> cmake::Generator
    {
        create_cmake_generator(self.compiler.as_ref().unwrap(), self.platform.as_ref().unwrap())
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
        
        if self.compiler == None
        {
            printer.error("Compiler not set");
            status = false;
        };
        
        if self.platform == None
        {
            printer.error("Platform not set");
            status = false;
        };

        status
    }

    pub fn save_to_file(&self, path: &Path)
    {
        let data = serde_json::to_string(&self).unwrap();
        fs::write(path, data).expect("Unable to write file");
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
                    if let Some(pr) = printer
                    {
                        pr.error(format!("Unable to parse json {}: {}", path.to_string_lossy(), error).as_str());
                    }
                    BuildEnviroment::new_empty()
                }
            }
        },
        None => BuildEnviroment::new_empty()
    }
}
