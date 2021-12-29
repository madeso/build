use std::path::Path;
use std::fs::{self, File};
use std::io::prelude::*;
use std::io::{self, BufRead};

use futures::executor::block_on;
extern crate reqwest;

use crate::
{
    printer
};


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
    cfg!(target_pointer_width = "64")
}

pub fn is_windows() -> bool
{
    cfg!(target_os = "windows")
}

/// make sure directory exists 
pub fn verify_dir_exist(print: &mut printer::Printer, dir: &Path)
{
    if dir.exists()
    {
        print.info(format!("Dir exist, not creating {}", dir.display()).as_str());
    }
    else
    {
        print.info(format!("Not a directory, creating {}", dir.display()).as_str());
        if let Err(e) = fs::create_dir_all(dir)
        {
            print.error(format!("Failed to create directory {}: {}", dir.display(), e).as_str());
        }
    }
}

/// download file if not already downloaded 
pub fn download_file(print: &mut printer::Printer, url: &str, dest: &Path)
{
    if dest.exists()
    {
        print.info(format!("Already downloaded {}", dest.display()).as_str());
    }
    else
    {
        print.info(format!("Downloading {}", dest.display()).as_str());
        let future = download_file_async(print, url, dest);
        block_on(future);
    }
}

async fn download_file_async(print: &mut printer::Printer, url: &str, dest: &Path)
{
    let resp = match reqwest::get(url).await
    {
        Ok(r) => r,
        Err(error) =>
        {
            print.error(format!("request start failed({}): {}", url, error).as_str());
            return;
        }
    };

    let data = match resp.bytes().await
    {
        Ok(r) => r,
        Err(error) =>
        {
            print.error(format!("request get failed({}): {}", url, error).as_str());
            return;
        }
    };

    let mut buffer = match File::create(dest)
    {
        Ok(r) => r,
        Err(error) =>
        {
            print.error(format!("failed to create file({}): {}", dest.display(), error).as_str());
            return;
        }
    };

    let mut pos = 0;

    while pos < data.len() {
        let bytes_written = match buffer.write(&data[pos..])
        {
            Ok(r) => r,
            Err(error) =>
            {
                print.error(format!("failed to copy content({} -> {}): {}", url, dest.display(), error).as_str());
                return;
            }
        };
        pos += bytes_written;
    }
}

/// moves all file from one directory to another
pub fn move_files(print: &mut printer::Printer, from: &Path, to: &Path)
{
    if from.exists() == false
    {
        print.error(format!("Missing src {} when moving to {}", from.display(), to.display()).as_str());
        return;
    }

    verify_dir_exist(print, to);
    if let Err(error) = move_files_rec(from, to)
    {
        print.error(format!("Failed to move {} to {}: {}", from.display(), to.display(), error).as_str());
    }
}

pub fn move_files_rec(from: &Path, to: &Path) -> std::io::Result<()>
{
    let paths = fs::read_dir(from)?;
    for file_path in paths
    {
        let pp = file_path?.path();
        let path = pp.as_path();

        let mut dst = to.to_path_buf();
        dst.push
        (
            match path.file_name()
            {
                Some(p) => Path::new(p),
                None => Path::new(path.file_stem().unwrap())
            }
        );

        if path.is_file()
        {
            // println!("{}{}", start, file);
            fs::rename(path, dst)?;
        }
        else
        {
            fs::create_dir_all(&dst)?;
            move_files_rec(path, &dst)?;
        }
    }

    Ok(())
}

/// extract a zip file to folder
pub fn extract_zip(_zip: &Path, _to: &Path)
{
    // todo(Gustav): implement me!
}


struct SingleReplacement
{
    old: String,
    new: String
}


/// multi replace calls on a single text 
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
