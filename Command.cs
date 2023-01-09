namespace Workbench;

using System.Diagnostics;


public class Command
{
    private int wait_for_exit(Printer print)
    {
        // Prepare the process to run
        ProcessStartInfo start = new()
        {
            Arguments = CollectArguments(),
            FileName = app,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory
        };

        using (Process? proc = Process.Start(start))
        {
            if(proc == null)
            {
                print.error("Unable to run command");
                return -1;
            }
            proc.WaitForExit();

            var stdout = proc.StandardOutput.ReadToEnd().TrimEnd();
            var stderr = proc.StandardError.ReadToEnd().TrimEnd();

            if(string.IsNullOrWhiteSpace(stdout) == false)
            {
                print.info(stdout);
            }
            if(string.IsNullOrWhiteSpace(stderr) == false)
            {
                print.info(stderr);
            }

            return proc.ExitCode;
        }
    }

#if false

pub fn check_call(print: &mut printer::Printer, cmd: &mut Command)
{
    let ret = wait_for_exit(print, cmd);
    print.info(format!("Return value: {}", ret).as_str());
    if ret != 0
    {
        print.error(format!("Failed to run command: {:?}", cmd).as_str());
    }
}
#endif
    private readonly string app;

    public Command(string app)
    {
        this.app = app;
    }

    List<string> arguments = new();

    private string workingDirectory = "";

    internal void arg(string argument)
    {
        arguments.Add(argument);
    }

    private string CollectArguments()
    {
        var args = arguments.ToArray();
        // https://stackoverflow.com/a/10489920
        string result = "";

        if (Environment.OSVersion.Platform == PlatformID.Unix
            ||
            Environment.OSVersion.Platform == PlatformID.MacOSX)
        {
            foreach (string arg in args)
            {
                result += (result.Length > 0 ? " " : "")
                    + arg
                        .Replace(@" ", @"\ ")
                        .Replace("\t", "\\\t")
                        .Replace(@"\", @"\\")
                        .Replace(@"""", @"\""")
                        .Replace(@"<", @"\<")
                        .Replace(@">", @"\>")
                        .Replace(@"|", @"\|")
                        .Replace(@"@", @"\@")
                        .Replace(@"&", @"\&");
            }
        }
        else //Windows family
        {
            bool enclosedInApo, wasApo;
            string subResult;
            foreach (string arg in args)
            {
                enclosedInApo = arg.LastIndexOfAny(
                    new char[] { ' ', '\t', '|', '@', '^', '<', '>', '&' }) >= 0;
                wasApo = enclosedInApo;
                subResult = "";
                for (int i = arg.Length - 1; i >= 0; i--)
                {
                    switch (arg[i])
                    {
                        case '"':
                            subResult = @"\""" + subResult;
                            wasApo = true;
                            break;
                        case '\\':
                            subResult = (wasApo ? @"\\" : @"\") + subResult;
                            break;
                        default:
                            subResult = arg[i] + subResult;
                            wasApo = false;
                            break;
                    }
                }
                result += (result.Length > 0 ? " " : "")
                    + (enclosedInApo ? "\"" + subResult + "\"" : subResult);
            }
        }

        return result;
    }

    internal void check_call(Printer printer)
    {
        throw new NotImplementedException();
    }

    internal void current_dir(string build_folder)
    {
        workingDirectory = build_folder;
    }
}