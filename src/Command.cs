namespace Workbench;

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

internal class CommandResult
{
    public int ExitCode { get; init; }
    public string stdout { get; init; }

    public CommandResult(int exitCode, string stdout)
    {
        this.ExitCode = exitCode;
        this.stdout = stdout;
    }

    public void Print(Printer print)
    {
        if (string.IsNullOrWhiteSpace(stdout) == false)
        {
            print.info(stdout);
        }
    }
}

public class Command
{
    internal CommandResult wait_for_exit(Printer print)
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

        var proc = new Process { StartInfo = start };
        var stdout = new StringBuilder();
        proc.OutputDataReceived += (sender, e) => { stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived += (sender, e) => { stdout.AppendLine(e.Data); };
        proc.Start();

        // required?
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        proc.WaitForExit();
        
        return new(proc.ExitCode, stdout.ToString());
    }

    private readonly string app;

    public Command(string app, params string[] args)
    {
        this.app = app;
        foreach(var a in args)
        {
            arg(a);
        }
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

    internal CommandResult check_call(Printer print)
    {
        var ret = wait_for_exit(print);
        print.info($"Return value: {ret}");
        if(ret.ExitCode != 0)
        {
            print.error($"Failed to run command: {this}");
        }

        ret.Print(print);

        return ret;
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