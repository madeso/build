use std::fs::File;
use std::io::{self, BufRead};
use std::path::Path;
use std::fs;


pub struct Printer
{
    error_count: i32,
    errors: Vec<String>
}

impl Printer
{
    pub fn new() -> Printer
    {
        Printer
        {
            error_count: 0,
            errors: Vec::<String>::new()
        }
    }

    // print a "pretty" header to the terminal
    pub fn header(&self, project_name: &str) { self.header_with_custom_char(project_name, "-"); }
    fn header_with_custom_char(&self, project_name: &str, header_character: &str)
    {
        // todo(Gustav): replace len with https://stackoverflow.com/a/46290728 if needed
        // or with https://users.rust-lang.org/t/fill-string-with-repeated-character/1121/9

        let header_size = 65;
        let header_spacing = 1;
        let header_start = 3;

        let spacing_string = " ".repeat(header_spacing);
        let header_string = header_character.repeat(header_size);

        let project = format!("{}{}{}", spacing_string, project_name, spacing_string);
        let start = header_character.repeat(header_start);

        let left = header_size - (project.len() + header_start);
        let right =
            if left > 1 { header_character.repeat(left) }
            else {String::from("")}
            ;
        
        println!("{}", header_string);
        println!("{}{}{}", start, project, right);
        println!("{}", header_string);
    }

    pub fn info(&self, text: &str)
    {
        println!("{}", text);
    }
    
    pub fn error(&mut self, text: &str)
    {
        self.error_count += 1;
        self.errors.push(String::from(text));
        println!("ERROR: {}", text);
    }

    // print the contents of a single file
    pub fn cat(&self, path: &str)
    {
        if let Ok(lines) = read_lines(path)
        {
            println!("{}>", path);
            for line in lines
            {
                if let Ok(ip) = line
                {
                    println!("---->{}", ip);
                }
            }
        }
        else
        {
            println!("Failed to open '{}'", path);
        }
    }

    // print files and folder recursivly
    pub fn ls(&self, root: &str) { self.ls_recursive(root, ""); }
    fn ls_recursive(&self, root: &str, start: &str)
    {
        let ident = " ".repeat(4);

        let paths = fs::read_dir(root).unwrap();
        for file_path in paths
        {
            let pp = file_path.unwrap().path();
            let path = pp.as_path();
            let file = path.file_name().unwrap().to_str().unwrap();

            if path.is_file()
            {
                println!("{}{}", start, file);
            }
            else
            {
                println!("{}{}/", start, file);
                self.ls_recursive(path.to_str().unwrap(), format!("{}{}", start, ident).as_str());
            }
        }
    }

    pub fn exit_with_code(&self)
    {
        if self.error_count > 0
        {
            println!("Errors detected: ({})", self.error_count)
        }

        for error in &self.errors
        {
            println!("{}", error);
        }
    }
}


// The output is wrapped in a Result to allow matching on errors
// Returns an Iterator to the Reader of the lines of the file.
fn read_lines<P>(filename: P) -> io::Result<io::Lines<io::BufReader<File>>> where P: AsRef<Path>,
{
    let file = File::open(filename)?;
    Ok(io::BufReader::new(file).lines())
}
