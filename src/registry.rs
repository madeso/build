// access registry on windows, returns None on non-windows

#[cfg(target_os = "windows")]
pub fn hklm(key_name: &str, value_name: &str) -> Result<String, String>
{
    Err(String::from("Not implemented."))
    // use https://crates.io/crates/winreg
}

#[cfg(not(target_os = "windows"))]
pub fn hklm(_: &str, _: & str) -> Result<String, String>
{
    Err(String::from("Not on windows"))
}
