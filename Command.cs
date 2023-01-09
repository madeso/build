namespace Workbench;

using System.Diagnostics;
using System.Security.Cryptography;

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

    internal void check_call(Printer print)
    {
        var ret = wait_for_exit(print);
        print.info($"Return value: {ret}");
        if(ret != 0)
        {
            print.error($"Failed to run command: {this}");
        }
    }

    internal void current_dir(string build_folder)
    {
        workingDirectory = build_folder;
    }

    public override string ToString()
    {
        var args = CollectArguments();
        return $"{app} {args}";
    }
}