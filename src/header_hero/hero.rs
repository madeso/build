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
pub enum Args
{
    /// Runs a "header hero" project and output a html report
    Html
    {
        #[structopt(flatten)]
        options: Options
    },

    // Runs a "header hero" project and output a graphviz/dot compatible map
    Dot
    {
        #[structopt(flatten)]
        options: DotOptions
    },
}


#[derive(StructOpt, Debug)]
pub struct Options
{
    /// Path to the header hero project file (json)
    input: PathBuf,

    /// Path to the html output directory
    output: PathBuf
}

#[derive(StructOpt, Debug)]
pub struct DotOptions
{
    /// Path to the header hero project file (json)
    input: PathBuf,

    /// Path to the graphviz output file
    output: PathBuf,

    /// Simplify the graphiz output?
    #[structopt(short, long)]
    simplify: bool,

    /// Display only headers in the output
    #[structopt(long)]
    only_headers: bool,

    /// Cluster files based on parent folder
    #[structopt(long)]
    cluster: bool,

    /// Exclude some files
    #[structopt(long)]
    exclude: Vec<PathBuf>
}


pub fn main(print: &mut printer::Printer, args: &Args)
{
    print.info("hello hero");

    match args
    {
        Args::Html {options} =>
        {
            run_html(print, options)
        },

        Args::Dot {options} =>
        {
            run_dot(print, options)
        }
    }
}

fn run_html(print: &mut printer::Printer, args: &Options)
{
    match data::UserInput::load_from_file(&args.input)
    {
        Ok(mut input) =>
        {
            match std::env::current_dir()
            {
                Ok(cwd) => input.decorate(&cwd),
                Err(_) => {}
            };

            if fs::create_dir_all(&args.output).is_err()
            {
                print.error(&format!("Failed to generate directories: {}", &args.output.display()));
            }
            else
            {
                ui::scan_and_generate_html(&input, &args.output);
            }
        }

        Err(error) =>
        {
            print.error(&error);
        }
    }
}


fn run_dot(print: &mut printer::Printer, args: &DotOptions)
{
    match data::UserInput::load_from_file(&args.input)
    {
        Ok(mut input) =>
        {
            match std::env::current_dir()
            {
                Ok(cwd) => input.decorate(&cwd),
                Err(_) => {}
            };

            ui::scan_and_generate_dot(print, &input, args.simplify, &args.output, args.only_headers, &args.exclude, args.cluster);
        }

        Err(error) =>
        {
            print.error(&error);
        }
    }
}
