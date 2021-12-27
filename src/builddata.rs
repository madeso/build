use std::path::{Path, PathBuf};
use std::env;

use regex::Regex;
use serde::{Serialize, Deserialize};

use crate::
{
    build,
    core,
    checkincludes,
    printer
};


#[derive(Serialize, Deserialize, Debug)]
struct ProjectFile
{
    name: String,
    dependencies: Vec<build::DependencyName>,
    includes: Vec<String>
}


pub enum OptionalRegex
{
    DynamicRegex(String),
    StaticRegex(Regex),
    FailedRegex(String)
}

// #[derive(Debug)]
pub struct BuildData
{
    pub name: String,
    pub dependencies: Vec<Box<dyn build::Dependency>>,
    pub root_dir: PathBuf,
    pub build_base_dir: PathBuf,
    pub build_dir: PathBuf,
    pub dependency_dir: PathBuf,
    pub includes: Vec<OptionalRegex>
}

impl BuildData
{
    fn new(name: &str, root_dir: &Path, includes: &[String], print: &mut printer::Printer) -> BuildData
    {
        let mut build_base_dir = root_dir.to_path_buf(); build_base_dir.push("build");
        let mut build_dir = root_dir.to_path_buf(); build_dir.push("build"); build_dir.push(name);
        let mut dependency_dir = root_dir.to_path_buf(); dependency_dir.push("build"); dependency_dir.push("deps");

        let replacer = checkincludes::get_replacer("file_stem");
        
        BuildData
        {
            name: name.to_string(),
            dependencies: vec!(),
            root_dir: root_dir.to_path_buf(),
            build_base_dir,
            build_dir,
            dependency_dir,
            includes: includes.iter().map
            (
                |regex|
                {
                    let regex_source = replacer.replace(regex);
                    if regex_source.eq(regex) == false
                    {
                        OptionalRegex::DynamicRegex(regex.to_string())
                    }
                    else
                    {
                        match Regex::new(&regex_source)
                        {
                            Ok(re) =>
                            {
                                OptionalRegex::StaticRegex(re)
                            },
                            Err(err) =>
                            {
                                let error = format!("{} is invalid regex: {}", regex, err);
                                print.error(error.as_str());
                                OptionalRegex::FailedRegex(error)
                            }
                        }
                    }
                }
            ).collect()
        }
    }

    // get the path to the settings file
    pub fn get_path_to_settings(&self) -> PathBuf
    {
        let mut settings = self.build_base_dir.clone();
        settings.push("settings.json");
        settings
    }
}


fn load_from_dir(root: &Path, print: &mut printer::Printer) -> Result<BuildData, String>
{
    let mut file = PathBuf::new();
    file.push(root);
    file.push("project.wb.json");
    let content = core::read_file_to_string(&file).ok_or(format!("Unable to read file: {}", file.to_string_lossy()))?;
    let data : Result<ProjectFile, serde_json::error::Error> = serde_json::from_str(&content);
    match data
    {
        Ok(loaded) =>
        {
            let mut bd = BuildData::new(&loaded.name, root, &loaded.includes, print);
            for dependency_name in loaded.dependencies
            {
                bd.dependencies.push(build::create(&dependency_name, &bd));
            }
            Ok(bd)
        },
        Err(error) => Err(format!("Unable to parse json {}: {}", file.to_string_lossy(), error))
    }
}


pub fn load(print: &mut printer::Printer) -> Result<BuildData, String>
{
    let path = env::current_dir().ok().ok_or("unable to get current directory")?;
    load_from_dir(&path, print)
}
