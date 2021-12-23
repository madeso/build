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

