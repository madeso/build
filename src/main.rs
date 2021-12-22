mod printer;

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
}


fn handle_demo(print: &mut printer::Printer)
{
    print.header("demo header");
    print.info("info");
    print.error("something failed");
}

fn main() {
    let args = WorkbenchArguments::from_args();
    let mut print = printer::Printer::new();
    
    println!("{:#?}", args);

    match args
    {
          WorkbenchArguments::Ls{path} => print.ls(path.as_str())
        , WorkbenchArguments::Cat{path} => print.cat(path.as_str())
        , WorkbenchArguments::Demo{} => handle_demo(&mut print)
    }
    

    print.exit_with_code()
}
