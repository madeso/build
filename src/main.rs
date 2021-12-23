mod printer;
mod registry;
mod cmake;
mod found;
mod builddata;
mod core;
mod buildenv;

use structopt::StructOpt;


#[derive(StructOpt, Debug)]
enum Build
{
    Status {}
}


#[derive(StructOpt, Debug)]
#[structopt(name = "wb")]
enum WorkbenchArguments
{
    Ls
    {
        #[structopt(parse(from_str))]
        path: String
    }
    , Cat
    {
        #[structopt(parse(from_str))]
        path: String
    }
    , Demo {}
    , Debug {}
    , Build(Build)
}


fn handle_demo(print: &mut printer::Printer)
{
    print.header("demo header");
    print.info("info");
    print.error("something failed");
}

fn print_found_list(printer: &printer::Printer, name: &str, list: &Vec::<found::Found>)
{
    let found = match found::first_value_or_none(list)
    {
        Some(res) => format!("{}", res),
        None => "<None>".to_string()
    };
    printer.info(format!("{}: {}", name, found).as_str());
    for f in list
    {
        printer.info(format!("    {}", f).as_str());
    }
}

fn handle_debug(printer: &mut printer::Printer)
{
    let cmakes = cmake::list_all(printer);
    print_found_list(printer, "cmake", &cmakes);
}


fn handle_build_status(printer: &mut printer::Printer)
{
    let loaded_data = builddata::load();
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
}


fn main() {
    let args = WorkbenchArguments::from_args();
    let mut print = printer::Printer::new();

    match args
    {
          WorkbenchArguments::Ls{path} => print.ls(path.as_str())
        , WorkbenchArguments::Cat{path} => print.cat(path.as_str())
        , WorkbenchArguments::Demo{} => handle_demo(&mut print)
        , WorkbenchArguments::Debug{} => handle_debug(&mut print)
        , WorkbenchArguments::Build(build) => match build
        {
            Build::Status{} => handle_build_status(&mut print)
        }
    }
    

    print.exit_with_code()
}
