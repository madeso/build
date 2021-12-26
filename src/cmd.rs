use std::process::Command;

use crate::
{
    printer
};

pub fn wait_for_exit(print: &mut printer::Printer, cmd: &mut Command) -> i32
{
    let output = cmd.output().expect("Unable to run command");

    let stdout = String::from_utf8_lossy(&output.stdout);
    let stderr = String::from_utf8_lossy(&output.stderr);

    if stdout.is_empty() == false
    {
        print.info(&stdout);
    }
    if stderr.is_empty() == false
    {
        print.info(&stderr);
    }

    match output.status.code()
    {
        Some(e) => { e }
        None =>
        {
            print.error("Process terminated by signal");
            -1
        }
    }
}

pub fn check_call(print: &mut printer::Printer, cmd: &mut Command)
{
    let ret = wait_for_exit(print, cmd);
    print.info(format!("Return value: {}", ret).as_str());
    if ret != 0
    {
        print.error(format!("Failed to run command: {:?}", cmd).as_str());
    }
}
