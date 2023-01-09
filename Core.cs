using System.Runtime.InteropServices;

namespace Workbench;

public static class Core
{
#if false

    use std::path::{Path, PathBuf};
    use std::fs::{self, File};
    use std::io::prelude::*;
    use std::io::{self, BufRead};
    use std::collections::VecDeque;

    extern crate reqwest;

    use thiserror::Error;

    use crate::
    {
        printer
    };


    // The output is wrapped in a Result to allow matching on errors
    // Returns an Iterator to the Reader of the lines of the file.
    pub fn read_file_to_lines_x<P>(filename: P) -> io::Result<io::Lines<io::BufReader<File>>>
    where P: AsRef<Path>, {
        let file = File::open(filename)?;
        Ok(io::BufReader::new(file).lines())
    }



    pub fn walk_files(dir: &Path) -> io::Result<Vec<PathBuf>>
    {
        let mut r = Vec::new();

        let mut dirs = VecDeque::new();
        dirs.push_back(dir.to_path_buf());

        while let Some(dir) = dirs.pop_front()
        {
            for entry in fs::read_dir(dir)?
            {
                let entry = entry?;
                let path = entry.path();
                if path.is_dir()
                {
                    dirs.push_back(path.to_path_buf());
                }
                else
                {
                    if path.is_file()
                    {
                        r.push(path.to_path_buf());
                    }
                }
            }
        }
        Ok(r)
    }



    pub fn file_get_extension(file: &Path) -> String
    {
        if let Some(ext) = file.extension()
        {
            ext.to_str().unwrap().to_string()
        }
        else
        {
            "".to_string()
        }
    }


    pub fn read_file_to_lines(path: &Path) -> io::Result<Vec<String>>
    {
        let lines : Vec<String> = read_file_to_lines_x(path)?.map(|l| l.unwrap()).collect();
        Ok(lines)
    }

    pub fn read_file_to_string_x(path: &Path) -> io::Result<String>
    {
        let mut file = File::open(path)?;
        let mut contents = String::new();
        file.read_to_string(&mut contents)?;
        Ok(contents)
    }


    pub fn read_file_to_string(path: &Path) -> Option<String>
    {
        read_file_to_string_x(path).ok()
    }

    pub fn write_string_to_file_or(path: &Path, data: &str) -> io::Result<()>
    {
        fs::write(path, data)
    }

    pub fn write_string_to_file(print: &mut printer::Printer, path: &Path, data: &str)
    {
        if write_string_to_file_or(path, data).is_err()
        {
            print.error(format!("Unable to save {}", path.display()).as_str());
        }
    }


    // check if the script is running on 64bit or not 
    pub fn is_64bit() -> bool
    {
        cfg!(target_pointer_width = "64")
    }
#endif
    public static bool is_windows()
    {
        return System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }
    

    /// make sure directory exists 
    public static void verify_dir_exist(Printer print, string dir)
    {
        if(Directory.Exists(dir))
        {
            print.info($"Dir exist, not creating {dir}");
        }
        else
        {
            print.info($"Not a directory, creating {dir}");
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch(Exception x)
            {
                print.error($"Failed to create directory {dir}: {x.Message}");
            }
        }
    }

#if false
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
            download_file_now(print, url, dest);
        }
    }

    fn download_file_now(print: &mut printer::Printer, url: &str, dest: &Path)
    {
        let resp = match reqwest::blocking::get(url)
        {
            Ok(r) => r,
            Err(error) =>
            {
                print.error(format!("request start failed({}): {}", url, error).as_str());
                return;
            }
        };

        let data = match resp.bytes()
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
    pub fn extract_zip(print: &mut printer::Printer, zip: &Path, to: &Path)
    {
        if let Err(err) = please_extract(print, zip, to)
        {
            print.error(format!("Failed to extract {} to {}: {}", zip.display(), to.display(), err).as_str());
        }
    }


    // #[derive(Error, Debug)]
    enum ZipError
    {
    // #[error(transparent)]
        Io(#[from] io::Error),

    // #[error(transparent)]
        Zip(#[from] zip::result::ZipError)
    }

    fn please_extract(print: &mut printer::Printer, fname: &Path, target_path: &Path) -> Result<(), ZipError>
    {
        let file = fs::File::open(&fname)?;
        let mut archive = zip::ZipArchive::new(file)?;

        for i in 0..archive.len()
        {
            let mut file = archive.by_index(i)?;
            let outpath_relative = match file.enclosed_name()
            {
                Some(path) => path.to_owned(),
                None => continue,
            };

            let mut outpath = target_path.to_path_buf();
            outpath.push(outpath_relative);

            {
                let comment = file.comment();
                if !comment.is_empty()
                {
                    print.info(format!("File {} comment: {}", i, comment).as_str());
                }
            }

            if (&*file.name()).ends_with('/')
            {
                print.info(format!("File {} extracted to \"{}\"", i, outpath.display()).as_str());
                fs::create_dir_all(&outpath)?;
            }
            else
            {
                print.info(format!
                (
                    "File {} extracted to \"{}\" ({} bytes)",
                    i,
                    outpath.display(),
                    file.size()
                ).as_str());
                if let Some(p) = outpath.parent()
                {
                    if !p.exists()
                    {
                        fs::create_dir_all(&p)?;
                    }
                }
                let mut outfile = fs::File::create(&outpath)?;
                io::copy(&mut file, &mut outfile)?;
            }

            // Get and Set permissions
            // #[cfg(unix)]
            {
                use std::os::unix::fs::PermissionsExt;

                if let Some(mode) = file.unix_mode()
                {
                    fs::set_permissions(&outpath, fs::Permissions::from_mode(mode))?;
                }
            }
        }

        Ok(())
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


    pub fn join(path: &Path, file: &str) -> PathBuf
    {
        let mut r = path.to_path_buf();
        r.push(file);
        r
    }

    extern crate chrono;
    use chrono::offset::Local;
    use chrono::DateTime;
    use std::time::SystemTime;

    pub fn display_time(system_time: SystemTime) -> String
    {
        //let system_time = SystemTime::now();
        let datetime: DateTime<Local> = system_time.into();
        datetime.format("%c").to_string()
    }
#endif
}