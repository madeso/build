#![allow(
    // sometimes comparing to true or false is more readable
    clippy::bool_comparison
)]
#![allow(
    // "too many arguments" is a potential code smell, not a style error
    // it also depends on the kind of function, setting a number globally is wrong
    // besides... it's my code, I decide what "too many arguments" are
    clippy::too_many_arguments
)]
#![allow(
    // understandable but sometimes you want to write "crappy" code because
    // the structure is there to be easily expanded
    clippy::single_match
)]

mod printer;
mod registry;
mod cmake;
mod found;
mod builddata;
mod core;
mod buildenv;
mod cmd;
mod build;
mod checkincludes;
mod listheaders;
mod compilecommands;

use structopt::StructOpt;


#[derive(StructOpt, Debug)]
enum Build
{
    Status
    {
        // #[structopt(flatten)]
        // env: buildenv::EnviromentArgument
    },

    /// Install dependencies
    Install
    {
        #[structopt(flatten)]
        env: buildenv::EnviromentArgument
    },

    /// Configure cmake project
    Cmake
    {
        #[structopt(flatten)]
        env: buildenv::EnviromentArgument,

        #[structopt(long)]
        print: bool
    },
    
    /// Dev is install+cmake
    Dev
    {
        #[structopt(flatten)]
        env: buildenv::EnviromentArgument
    },

    /// Build the project
    Build
    {
        #[structopt(flatten)]
        env: buildenv::EnviromentArgument
    }
}


#[derive(StructOpt, Debug)]
#[structopt(name = "wb")]
enum WorkbenchArguments
{
    /// Print the tree of a directory
    Ls
    {
        #[structopt(parse(from_str))]
        path: String
    },

    /// Print the contents of a single file
    Cat
    {
        #[structopt(parse(from_str))]
        path: String
    },

    /// Demo the print output
    Demo {},

    /// Display what workbench think of your current setup
    Debug
    {
        #[structopt(flatten)]
        cc: compilecommands::CompileCommandArg
    },

    /// Build commands
    Build(Build),

    /// List headers
    ListHeaders
    {
        #[structopt(flatten)]
        options: listheaders::Options
    },

    /// Tool to check the order of #include statements in files
    CheckIncludes
    {
        #[structopt(flatten)]
        options: checkincludes::Options
    },

    /// compile_commands.json utilities
    CompileCommands
    {
        #[structopt(flatten)]
        options: compilecommands::Options
    }
}


///////////////////////////////////////////////////////////////////////////////////////////////////


// generate the ride project
fn generate_cmake_project(build: &buildenv::BuildEnviroment, data: &builddata::BuildData) -> cmake::CMake
{
    let mut project = cmake::CMake::new(&data.build_dir, &data.root_dir, build.get_cmake_generator());

    for dep in &data.dependencies
    {
        dep.add_cmake_arguments(&mut project)
    }

    project
}


// install dependencies
fn run_install(env: &buildenv::BuildEnviroment, data: &builddata::BuildData, print: &mut printer::Printer)
{
    for dep in &data.dependencies
    {
        dep.install(env, print, data);
    }
}


// configure the euphoria cmake project
fn run_cmake(build: &buildenv::BuildEnviroment, data: &builddata::BuildData, printer: &mut printer::Printer, only_print: bool)
{
    generate_cmake_project(build, data).config_with_print(printer, only_print)
}


// save the build environment to the settings file
fn save_build(print: &mut printer::Printer, build: &buildenv::BuildEnviroment, data: &builddata::BuildData)
{
    core::verify_dir_exist(print, &data.build_base_dir);
    build.save_to_file(&data.get_path_to_settings())
}

///////////////////////////////////////////////////////////////////////////////////////////////////


fn handle_demo(print: &mut printer::Printer)
{
    print.header("demo header");
    print.info("info");
    print.error("something failed");
}

fn print_found_list(printer: &printer::Printer, name: &str, list: &[found::Found])
{
    let found = match found::first_value_or_none(list)
    {
        Some(res) => res,
        None => "<None>".to_string()
    };
    printer.info(format!("{}: {}", name, found).as_str());
    for f in list
    {
        printer.info(format!("    {}", f).as_str());
    }
}

fn handle_debug(printer: &mut printer::Printer, cc: &compilecommands::CompileCommandArg)
{
    let cmakes = cmake::list_all(printer);
    print_found_list(printer, "cmake", &cmakes);

    match cc.get_argument_or_none_with_cwd()
    {
        Some(found) => printer.info(format!("Compile commands: {}", found.display()).as_str()),
        None => printer.info("Compile commands: <NONE>")
    }
}


fn handle_build_status(printer: &mut printer::Printer)
{
    let loaded_data = builddata::load(printer);
    if let Err(err) = loaded_data
    {
        printer.error(format!("Unable to load the data: {}", err).as_str());
        return;
    }
    let data = loaded_data.unwrap();
    let env = buildenv::load_from_file(&data.get_path_to_settings(), Some(printer));

    printer.info(format!("Project: {}", data.name).as_str());
    printer.info(format!("Enviroment: {:?}", env).as_str());
    printer.info("");
    printer.info(format!("Data: {}", data.get_path_to_settings().to_string_lossy()).as_str());
    printer.info(format!("Root: {}", data.root_dir.to_string_lossy()).as_str());
    printer.info(format!("Build: {}", data.build_dir.to_string_lossy()).as_str());
    printer.info(format!("Dependencies: {}", data.dependency_dir.to_string_lossy()).as_str());
    let indent = " ".repeat(4);
    for dep in data.dependencies
    {
        printer.info(format!("{}{}", indent, dep.get_name()).as_str());
        let lines = dep.status();
        for line in lines
        {
            printer.info(format!("{}{}{}", indent, indent, line).as_str());
        }
    }
}

fn handle_generic_build<Callback>(printer: &mut printer::Printer, args: &buildenv::EnviromentArgument, callback: Callback)
    where Callback: Fn(&mut printer::Printer, &buildenv::BuildEnviroment, &builddata::BuildData)
{
    let loaded_data = builddata::load(printer);
    if let Err(err) = loaded_data
    {
        printer.error(format!("Unable to load the data: {}", err).as_str());
        return;
    }
    let data = loaded_data.unwrap();
    let mut env = buildenv::load_from_file(&data.get_path_to_settings(), Some(printer));
    env.update_from_args(printer, args);
    if env.validate(printer) == false
    {
        return;
    }

    callback(printer, &env, &data);
}


fn main() {
    let args = WorkbenchArguments::from_args();
    let mut print = printer::Printer::new();

    match args
    {
          WorkbenchArguments::Ls{path} => print.ls(path.as_str())
        , WorkbenchArguments::Cat{path} => print.cat(path.as_str())
        , WorkbenchArguments::Demo{} => handle_demo(&mut print)
        , WorkbenchArguments::Debug{cc} => handle_debug(&mut print, &cc)
        , WorkbenchArguments::Build(build) => match build
        {
            Build::Status{} => handle_build_status(&mut print),
            Build::Install{env} => handle_generic_build
                (
                    &mut print, &env,
                    |printer: &mut printer::Printer, build: &buildenv::BuildEnviroment, data: &builddata::BuildData|
                    {
                        save_build(printer, build, data);
                        run_install(build, data, printer);
                    }
                ),
            Build::Cmake{env, print: arg_print} => handle_generic_build
                (
                    &mut print, &env,
                    |printer: &mut printer::Printer, build: &buildenv::BuildEnviroment, data: &builddata::BuildData|
                    {
                        save_build(printer, build, data);
                        run_cmake(build, data, printer, arg_print);
                    }
                ),
            Build::Dev{env} => handle_generic_build
                (
                    &mut print, &env,
                    |printer: &mut printer::Printer, build: &buildenv::BuildEnviroment, data: &builddata::BuildData|
                    {
                        save_build(printer, build, data);
                        run_install(build, data, printer);
                        run_cmake(build, data, printer, false);
                    }
                ),
            Build::Build{env} => handle_generic_build
                (
                    &mut print, &env,
                    |printer: &mut printer::Printer, build: &buildenv::BuildEnviroment, data: &builddata::BuildData|
                    {
                        save_build(printer, build, data);
                        generate_cmake_project(build, data).build(printer);
                    }
                )
        }
        , WorkbenchArguments::ListHeaders{options} =>
        {
            let loaded_data = builddata::load(&mut print);
            if let Err(err) = loaded_data
            {
                print.error(format!("Unable to load the data: {}", err).as_str());
                return;
            }
            else
            {
                let data = loaded_data.unwrap();
                listheaders::main(&mut print, &data, &options)
            }
        }
        , WorkbenchArguments::CheckIncludes{options} =>
        {
            let loaded_data = builddata::load(&mut print);
            if let Err(err) = loaded_data
            {
                print.error(format!("Unable to load the data: {}", err).as_str());
                return;
            }
            else
            {
                let data = loaded_data.unwrap();
                checkincludes::main(&mut print, &data, &options)
            }
        }
        , WorkbenchArguments::CompileCommands{options} =>
        {
            compilecommands::main(&mut print, &options)
        }
    }
    

    print.exit_with_code()
}
