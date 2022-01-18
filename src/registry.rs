// access registry on windows, returns None on non-windows

extern crate winreg;
use std::io;
use std::path::Path;
use winreg::enums::*;
use winreg::RegKey;

#[cfg(target_os = "windows")]
pub fn hklm(key_name: &str, value_name: &str) -> io::Result<String>
{
    let hklm = RegKey::predef(HKEY_LOCAL_MACHINE);
    let cur_ver = hklm.open_subkey(key_name)?; // "SOFTWARE\\Microsoft\\Windows\\CurrentVersion"
    let value: String = cur_ver.get_value(value_name)?; // "ProgramFilesDir"
    Ok(value)
}

#[cfg(not(target_os = "windows"))]
pub fn hklm(_: &str, _: & str) -> Result<String, String>
{
    Err(String::from("Not on windows"))
}
