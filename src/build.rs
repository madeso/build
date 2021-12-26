use std::path::{Path, PathBuf};
use std::env;

use serde::{Serialize, Deserialize};

use crate::
{
    buildenv,
    builddata,
    core,
    cmake,
    printer
};


pub trait Dependency
{
    fn get_name(&self) -> &str;

    // add arguments to the main cmake
    fn add_cmake_arguments(&self, cmake: &mut cmake::CMake, env: &buildenv::BuildEnviroment);
    
    // install the dependency
    fn install(&self, env: &buildenv::BuildEnviroment, print: &mut printer::Printer, data: &builddata::BuildData);

    // get the status of the dependency
    fn status(&self) -> Vec<String>;
}

#[derive(Serialize, Deserialize, Debug)]
pub enum DependencyName
{
    #[serde(rename = "sdl2")]
    SDL2,

    #[serde(rename = "python")]
    PYTHON,

    #[serde(rename = "assimp")]
    ASSIMP
}

pub fn create(name: &DependencyName, data: &builddata::BuildData) -> Box<dyn Dependency>
{
    match name
    {
        DependencyName::SDL2 => Box::new(Dependency_Sdl2::new(data)),
        DependencyName::PYTHON => Box::new(Dependency_Python::new()),
        DependencyName::ASSIMP => Box::new(Dependency_Assimp::new(data))
    }
}

///////////////////////////////////////////////////////////////////////////////////////////////////

struct Dependency_Sdl2
{
    root_folder: PathBuf,
    build_folder: PathBuf
}

// add sdl2 dependency
impl Dependency_Sdl2
{
    fn new(data: &builddata::BuildData) -> Dependency_Sdl2
    {
        let mut root_folder = data.dependency_dir.clone(); root_folder.push("sdl2");
        let mut build_folder = root_folder.clone(); build_folder.push("cmake-build");
        
        Dependency_Sdl2
        {
            root_folder,
            build_folder
        }
    }
}

fn join(path: &Path, file: &str) -> PathBuf
{
    let mut r = path.to_path_buf();
    r.push(file);
    r
}

impl Dependency for Dependency_Sdl2
{
    fn get_name(&self) -> &str
    {
        "sdl2"
    }

    fn add_cmake_arguments(&self, cmake: &mut cmake::CMake, env: &buildenv::BuildEnviroment)
    {
        cmake.add_argument("SDL2_HINT_ROOT".to_string(), self.root_folder.to_string_lossy().to_string());
        cmake.add_argument("SDL2_HINT_BUILD".to_string(), self.build_folder.to_string_lossy().to_string());
    }
    
    fn install(&self, env: &buildenv::BuildEnviroment, print: &mut printer::Printer, data: &builddata::BuildData)
    {
        let deps = &data.dependency_dir;
        let root = &self.root_folder;
        let build = &self.build_folder;
        let generator = env.get_cmake_generator();

        // btdeps.install_dependency_sdl2(data.dependency_dir, root_folder, build_folder, build.get_generator())
        // def install_dependency_sdl2(deps, root, build, generator: cmake.Generator):

        print.header("Installing dependency sdl2");
        let url = "https://www.libsdl.org/release/SDL2-2.0.8.zip";
        
        let zip_file = join(&deps, "sdl2.zip");
        
        if false == zip_file.exists()
        {
            core::verify_dir_exist(&root);
            core::verify_dir_exist(&deps);
            print.info("downloading sdl2");
            core::download_file(url, &zip_file);
        }
        else
        {
            print.info("SDL2 zip file exist, not downloading again...");
        }

        if false == join(&root, "INSTALL.txt").exists()
        {
            core::extract_zip(&zip_file, &root);
            core::move_files(&join(&root, "SDL2-2.0.8"), &root);
        }
        else
        {
            print.info("SDL2 is unzipped, not unzipping again");
        }
        
        if false == join(&build, "SDL2.sln").exists()
        {
            let mut project = cmake::CMake::new(&build, &root, generator);
            // project.make_static_library()
            // this is defined by the standard library so don't add it
            // generates '__ftol2_sse already defined' errors
            project.add_argument("LIBC".to_string(), "ON".to_string());
            project.add_argument("SDL_STATIC".to_string(), "ON".to_string());
            project.add_argument("SDL_SHARED".to_string(), "OFF".to_string());
            project.config(print);
            project.build(print);
        }
        else
        {
            print.info("SDL2 build exist, not building again...")
        }
    }

    fn status(&self) -> Vec<String>
    {
        vec!
        (
            format!("Root: {}", self.root_folder.to_string_lossy()),
            format!("Build: {}", self.build_folder.to_string_lossy())
        )
    }
}

///////////////////////////////////////////////////////////////////////////////////////////////////

struct Dependency_Python
{
    python_env: Result<String, std::env::VarError>
}

// add python dependency

impl Dependency_Python
{
    fn new() -> Dependency_Python
    {
        Dependency_Python
        {
            python_env: env::var("PYTHON")
        }
    }
}

impl Dependency for Dependency_Python
{
    fn get_name(&self) -> &str
    {
        "python"
    }

    fn add_cmake_arguments(&self, cmake: &mut cmake::CMake, env: &buildenv::BuildEnviroment)
    {
        if let Ok(environ) = &self.python_env
        {
            let mut python_exe = PathBuf::from(environ); python_exe.push("python.exe");
            cmake.add_argument("PYTHON_EXECUTABLE:FILEPATH".to_string(), python_exe.to_string_lossy().to_string())
        }
    }
    
    fn install(&self, env: &buildenv::BuildEnviroment, print: &mut printer::Printer, data: &builddata::BuildData)
    {
    }
    
    fn status(&self) -> Vec<String>
    {
        vec!
        (
            match &self.python_env
            {
                Ok(val) => format!("PYTHON: {:?}", val),
                Err(e) => format!("Couldn't interpret PYTHON: {}", e),
            }
        )
    }
}

///////////////////////////////////////////////////////////////////////////////////////////////////

struct Dependency_Assimp
{
    assimp_folder: PathBuf,
    assimp_install_folder: PathBuf
}


// add assimp dependency
impl Dependency_Assimp
{
    fn new(data: &builddata::BuildData) -> Dependency_Assimp
    {
        let mut assimp_folder = data.dependency_dir.clone(); assimp_folder.push("assimp");
        let mut assimp_install_folder = assimp_folder.clone(); assimp_install_folder.push("cmake-install");
        
        Dependency_Assimp
        {
            assimp_folder,
            assimp_install_folder
        }
    }
}

impl Dependency for Dependency_Assimp
{
    fn get_name(&self) -> &str
    {
        "assimp"
    }

    fn add_cmake_arguments(&self, cmake: &mut cmake::CMake, env: &buildenv::BuildEnviroment)
    {
        cmake.add_argument("ASSIMP_ROOT_DIR".to_string(), self.assimp_install_folder.to_string_lossy().to_string());
    }
    
    
    fn install(&self, env: &buildenv::BuildEnviroment, print: &mut printer::Printer, data: &builddata::BuildData)
    {
        let url = "https://github.com/assimp/assimp/archive/v5.0.1.zip";

        let deps = &data.dependency_dir;
        let root = &self.assimp_folder;
        let install = &self.assimp_install_folder;
        let generator = env.get_cmake_generator();

        // btdeps.install_dependency_assimp(data.dependency_dir, assimp_folder, assimp_install_folder, build.get_generator())
        // def install_dependency_assimp(deps: str, root: str, install: str, generator: cmake.Generator):
        print.header("Installing dependency assimp");
        let zip_file = join(&deps, "assimp.zip");
        if false == root.exists()
        {
            core::verify_dir_exist(&root);
            core::verify_dir_exist(&deps);
            print.info("downloading assimp");
            core::download_file(url, &zip_file);
            print.info("extracting assimp");
            core::extract_zip(&zip_file, &root);
            let build = join(&root, "cmake-build");
            core::move_files(&join(&root, "assimp-5.0.1"), &root);
            
            let mut project = cmake::CMake::new(&build, &root, generator);
            project.add_argument("ASSIMP_BUILD_X3D_IMPORTER".to_string(), "0".to_string());
            // project.make_static_library();
            print.info(format!("Installing cmake to {}", install.to_string_lossy().to_string()).as_str());
            project.set_install_folder(&install);
            core::verify_dir_exist(&install);
            
            project.config(print);
            project.build(print);
            
            print.info("Installing assimp");
            project.install(print);
        }
        else
        {
            print.info("Assimp build exist, not building again...");
        }
    }
    

    fn status(&self) -> Vec<String>
    {
        vec!
        (
            format!("Root: {}", self.assimp_folder.to_string_lossy()),
            format!("Install: {}", self.assimp_install_folder.to_string_lossy())
        )
    }
}

