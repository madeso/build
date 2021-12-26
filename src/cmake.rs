// expose cmake utilities

// subprocess
// typing
// shutil
// os
// shlex
// core
use std::path::{Path, PathBuf};
use std::process::Command;

use crate::
{
    core,
    cmd,
    found,
    registry,
    printer
};


fn find_cmake_in_registry(printer: &mut printer::Printer) -> found::Found
{
    let registry_source = "registry".to_string();

    match registry::hklm(r"SOFTWARE\Kitware\CMake", "InstallDir")
    {
        Err(_) => found::Found::new(None, registry_source),
        Ok(install_dir) =>
        {
            let bpath: PathBuf = [install_dir.as_str(), "bin", "cmake.exe"].iter().collect();
            let spath = bpath.as_path();
            let path = spath.to_str().unwrap();
            if spath.exists()
            {
                found::Found::new(Some(path.to_string()), registry_source)
            }
            else
            {
                printer.error(format!("Found path to cmake in registry ({}) but it didn't exist", path).as_str());
                found::Found::new(None, registry_source)
            }
        }
    }
}


fn find_cmake_in_path(printer: &mut printer::Printer) -> found::Found
{
    let path_source = "path".to_string();
    match which::which("cmake")
    {
        Err(_) => found::Found::new(None, path_source),
        Ok(bpath) =>
        {
            let spath = bpath.as_path();
            let path = spath.to_str().unwrap();
            if spath.exists()
            {
                found::Found::new(Some(path.to_string()), path_source)
            }
            else
            {
                printer.error(format!("Found path to cmake in path ({}) but it didn't exist", path).as_str());
                found::Found::new(None, path_source)
            }
        }
    }
}


pub fn list_all(printer: &mut printer::Printer) -> Vec::<found::Found>
{
    vec![find_cmake_in_registry(printer), find_cmake_in_path(printer)]
}


fn find_cmake_executable(printer: &mut printer::Printer) -> Option<String>
{
    let list = list_all(printer);
    found::first_value_or_none(&list)
}


// a cmake argument
struct Argument
{
    name: String,
    value: String,
    typename: Option<String>
}

impl Argument
{
    pub fn new(name: String, value: String, typename: Option<String>) -> Argument
    {
        Argument
        {
            name,
            value,
            typename
        }
    }

    // format for commandline
    pub fn format_cmake_argument(&self) -> String
    {
        match &self.typename
        {
            Some(t) => format!("-D{}:{}={}", self.name, t, self.value),
            None => format!("-D{}={}", self.name, self.value)
        }
    }
}


// cmake generator
pub struct Generator
{
    generator: String,
    arch: Option<String>
}

impl Generator
{
    pub fn new_with_arch(generator: &str, arch: &str) -> Generator
    {
        Generator
        {
            generator: generator.to_string(),
            arch: Some(arch.to_string())
        }
    }
    pub fn new(generator: &str) -> Generator
    {
        Generator
        {
            generator: generator.to_string(),
            arch: None
        }
    }
}


// utility to call cmake commands on a project
pub struct CMake
{
    generator: Generator,
    build_folder: PathBuf,
    source_folder: PathBuf,
    arguments: Vec::<Argument>
}


impl CMake
{
    pub fn new(build_folder: &Path, source_folder: &Path, generator: Generator) -> CMake
    {
        CMake
        {
            generator,
            build_folder: build_folder.to_path_buf(),
            source_folder: source_folder.to_path_buf(),
            arguments: Vec::<Argument>::new()
        }
    }

    // add argument with a explicit type set
    pub fn add_argument_with_type(&mut self, name: String, value: String, typename: String)
    {
        self.arguments.push(Argument::new(name, value, Some(typename)))
    }

    // add argument
    pub fn add_argument(&mut self, name: String, value: String)
    {
        self.arguments.push(Argument::new(name, value, None))
    }

    // set the install folder
    pub fn set_install_folder(&mut self, folder: &Path)
    {
        self.add_argument_with_type("CMAKE_INSTALL_PREFIX".to_string(), folder.to_string_lossy().to_string(), "PATH".to_string())
    }

    // set cmake to make static (not shared) library
    pub fn make_static_library(&mut self)
    {
        self.add_argument("BUILD_SHARED_LIBS".to_string(), "0".to_string())
    }

    // run cmake configure step
    pub fn config(&self, printer: &mut printer::Printer) { self.config_with_print(printer, false); }
    pub fn config_with_print(&self, printer: &mut printer::Printer, only_print: bool)
    {
        let found_cmake = find_cmake_executable(printer);
        let cmake = match found_cmake
        {
            Some(f) => {f},
            None =>
            {
                printer.error("CMake executable not found");
                return;
            }
        };
        
        let mut command = Command::new(cmake);
        for arg in &self.arguments
        {
            let argument = arg.format_cmake_argument();
            printer.info(format!("Setting CMake argument for config: {}", argument).as_str());
            command.arg(argument);
        }

        command.arg(self.source_folder.to_string_lossy().to_string());
        command.arg("-G");
        command.arg(self.generator.generator.as_str());
        match &self.generator.arch
        {
            Some(arch) =>
            {
                command.arg("-A");
                command.arg(arch);
            }
            None => {}
        }
        
        core::verify_dir_exist(&self.build_folder);
        command.current_dir(self.build_folder.to_string_lossy().to_string());
        
        if core::is_windows()
        {
            if only_print
            {
                printer.info(format!("Configuring cmake: {:?}", command).as_str());
            }
            else
            {
                // core::flush();
                cmd::check_call(printer, &mut command);
            }
        }
        else
        {
            printer.info(format!("Configuring cmake: {:?}", command).as_str());
        }
    }

    // run cmake build step
    pub fn build_cmd(&self, printer: &mut printer::Printer, install: bool)
    {
        let found_cmake = find_cmake_executable(printer);
        let cmake = match found_cmake
        {
            Some(f) => {f},
            None =>
            {
                printer.error("CMake executable not found");
                return;
            }
        };

        let mut command = Command::new(cmake);
        command.arg("--build");
        command.arg(".");

        if install
        {
            command.arg("--target");
            command.arg("install");
        }
        command.arg("--config");
        command.arg("Release");

        core::verify_dir_exist(&self.build_folder);
        command.current_dir(self.build_folder.to_string_lossy().to_string());

        if core::is_windows()
        {
            // core::flush()
            cmd::check_call(printer, &mut command);
        }
        else
        {
            printer.info(format!("Calling build on cmake: {:?}", command).as_str());
        }
    }

    // build cmake project
    pub fn build(&self, printer: &mut printer::Printer)
    {
        self.build_cmd(printer, false);
    }

    // install cmake project
    pub fn install(&self, printer: &mut printer::Printer)
    {
        self.build_cmd(printer, true);
    }
}
