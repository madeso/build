use std::path::Path;
use std::fs::File;
use std::io::prelude::*;
use std::io::{self, BufRead};

// The output is wrapped in a Result to allow matching on errors
// Returns an Iterator to the Reader of the lines of the file.
pub fn read_file_to_lines<P>(filename: P) -> io::Result<io::Lines<io::BufReader<File>>>
where P: AsRef<Path>, {
    let file = File::open(filename)?;
    Ok(io::BufReader::new(file).lines())
}


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


struct SingleReplacement
{
    old: String,
    new: String
}


// multi replace calls on a single text 
pub struct TextReplacer
{
    replacements: Vec<SingleReplacement>
}

impl TextReplacer
{
    pub fn new() -> TextReplacer
    {
        TextReplacer
        {
            replacements: Vec::<SingleReplacement>::new()
        }
    }

    // add a replacement command 
    pub fn add(&mut self, old: &str, new: &str)
    {
        self.replacements.push(SingleReplacement{old: old.to_string(), new: new.to_string()});
    }

    pub fn replace(&self, in_text: &str) -> String
    {
        let mut text = String::from(in_text);
        
        for replacement in &self.replacements
        {
            text = text.replace(&replacement.old, &replacement.new);
        }
        
        text
    }
}

