mod printer;
mod registry;
mod cmake;
mod found;

use structopt::StructOpt;

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
}


fn handle_demo(print: &mut printer::Printer)
{
    print.header("demo header");
    print.info("info");
    print.error("something failed");
}

fn print_found_list(printer: &printer::Printer, name: &str, list: &Vec::<found::Found>)
{
    printer.info(format!("{}:", name).as_str());
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

fn main() {
    let args = WorkbenchArguments::from_args();
    let mut print = printer::Printer::new();

    match args
    {
          WorkbenchArguments::Ls{path} => print.ls(path.as_str())
        , WorkbenchArguments::Cat{path} => print.cat(path.as_str())
        , WorkbenchArguments::Demo{} => handle_demo(&mut print)
        , WorkbenchArguments::Debug{} => handle_debug(&mut print)
    }
    

    print.exit_with_code()
}
