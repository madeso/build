use std::path::Path;
use std::fs::File;
use std::io::prelude::*;

pub fn read_file_to_string(path: &Path) -> Option<String>
{
    let mut file = File::open(path).ok()?;
    let mut contents = String::new();
    file.read_to_string(&mut contents).ok()?;
    Some(contents)
}


// check if the script is running on 64bit or not 
pub fn is_64bit() -> bool
{
    if cfg!(target_pointer_width = "64")
    {
        true
    }
    else
    {
        false
    }
}

pub fn is_windows() -> bool
{
    if cfg!(target_os = "windows")
    {
        true
    }
    else
    {
        false
    }
}

pub fn verify_dir_exist(_dir: &Path)
{
    // todo(Gustav): implement me!
}

pub fn download_file(_url: &str, _dest: &Path)
{
    // todo(Gustav): implement me!
}

pub fn move_files(_from: &Path, _to: &Path)
{
    // todo(Gustav): implement me!
}

pub fn extract_zip(_zip: &Path, _to: &Path)
{
    // todo(Gustav): implement me!
}
