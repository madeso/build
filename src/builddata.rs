use std::path::{Path, PathBuf};
use std::env;

use serde::{Serialize, Deserialize};

use crate::
{
    build,
    core
};


#[derive(Serialize, Deserialize, Debug)]
struct ProjectFile
{
    name: String,
    dependencies: Vec<build::DependencyName>,
    includes: Vec<String>
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
    pub includes: Vec<String>
}

impl BuildData
{
    fn new(name: &str, root_dir: &Path, includes: &Vec<String>) -> BuildData
    {
        let mut build_base_dir = root_dir.to_path_buf(); build_base_dir.push("build");
        let mut build_dir = root_dir.to_path_buf(); build_dir.push("build"); build_dir.push(name);
        let mut dependency_dir = root_dir.to_path_buf(); dependency_dir.push("build"); dependency_dir.push("deps");
        
        BuildData
        {
            name: name.to_string(),
            dependencies: vec!(),
            root_dir: root_dir.to_path_buf(),
            build_base_dir: build_base_dir,
            build_dir: build_dir,
            dependency_dir: dependency_dir,
            includes: includes.clone()
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


fn load_from_dir(root: &Path) -> Result<BuildData, String>
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
            let mut bd = BuildData::new(&loaded.name, root, &loaded.includes);
            for dependency_name in loaded.dependencies
            {
                bd.dependencies.push(build::create(&dependency_name, &bd));
            }
            Ok(bd)
        },
        Err(error) => Err(format!("Unable to parse json {}: {}", file.to_string_lossy(), error))
    }
}


pub fn load() -> Result<BuildData, String>
{
    let path = env::current_dir().ok().ok_or("unable to get current directory")?;
    load_from_dir(&path)
}
