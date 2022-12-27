// access registry on windows, returns None on non-windows

#[cfg(target_os = "windows")]
extern crate winreg;

#[cfg(target_os = "windows")]
use std::io;

#[cfg(target_os = "windows")]
pub fn hklm(key_name: &str, value_name: &str) -> io::Result<String>
{
    use winreg::enums::*;
    use winreg::RegKey;
    use std::path::Path;

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
