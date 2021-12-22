// expose cmake utilities

// subprocess
// typing
// shutil
// os
// shlex
// core
use std::path::PathBuf;

use crate::
{
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
