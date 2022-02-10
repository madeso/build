use std::fs;
use std::path::PathBuf;

use structopt::StructOpt;

use crate::
{
    printer,
    header_hero::ui,
    header_hero::data
};


#[derive(StructOpt, Debug)]
pub struct Options
{
    /// json project
    input: PathBuf,

    /// output directory
    output: PathBuf
}

pub fn main(print: &mut printer::Printer, args: &Options)
{
    print.info("hello hero");
    
    match data::UserInput::load_from_file(&args.input)
    {
        Ok(mut input) =>
        {
            match std::env::current_dir()
            {
                Ok(cwd) => input.decorate(&cwd),
                Err(_) => {}
            };

            if let Err(_) = fs::create_dir_all(&args.output)
            {
                print.error(&format!("Failed to generate directories: {}", &args.output.display()));
            }
            else
            {
                ui::scan_and_generate(&input, &args.output);
            }
        }

        Err(error) =>
        {
            print.error(&error);
        }
    }

}
