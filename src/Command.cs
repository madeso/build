namespace Workbench;

using System.Diagnostics;

internal record ProcessExit(string CommandLine, int ExitCode);

internal class CommandResult
{
    public string CommandLine { get; private init; }
    public int ExitCode { get; private init; }
    public string[] Output { get; private init; }

    public CommandResult(ProcessExit pe, string[] output)
    {
        CommandLine = pe.CommandLine;
        ExitCode = pe.ExitCode;
        this.Output = output;
    }

    public string[] RequireSuccess()
    {
        if (ExitCode != 0)
        {
            var outputString = string.Join('\n', Output);
            throw new Exception($"{CommandLine} has exit code {ExitCode}:\n{outputString}");
        }

        return this.Output;
    }

    public CommandResult PrintOutput(Printer print)
    {
        foreach (var line in this.Output)
        {
            print.Info(line);
        }

        return this;
    }

    public CommandResult PrintStatus(Printer print)
    {
        print.Info($"Return value: {ExitCode}");
        if (ExitCode != 0)
        {
            print.error($"Failed to run command: {CommandLine}");
        }

        return this;
    }

    public CommandResult PrintStatusAndUpdate(Printer print)
    {
        this.PrintStatus(print);
        this.PrintOutput(print);
        return this;
    }
}

public class Command
{
    internal ProcessExit RunWithCallback(Action<string> onLine)
    {
        // Prepare the process to run
        ProcessStartInfo start = new()
        {
            Arguments = CollectArguments(),
            FileName = Executable,
            UseShellExecute = false,

            WorkingDirectory = WorkingDirectory ?? Environment.CurrentDirectory,

            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        var proc = new Process { StartInfo = start };
        proc.OutputDataReceived += (sender, e) => { if (e.Data != null) { onLine(e.Data); } };
        proc.ErrorDataReceived += (sender, e) => { if (e.Data != null) { onLine(e.Data); } };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        proc.WaitForExit();

        return new(this.ToString(), proc.ExitCode);
    }

    internal CommandResult RunAndGetOutput()
    {
        var output = new List<string>();

        var ret = RunWithCallback(line => output.Add(line));

        return new(ret, output.ToArray());
    }

    private string Executable { get; }
    private readonly List<string> arguments = new();
    public string? WorkingDirectory { get; set; } = "";

    public Command(string executable, params string[] arguments)
    {
        this.Executable = executable;
        foreach (var arg in arguments)
        {
            AddArgument(arg);
        }
    }

    public Command InDirectory(string directory)
    {
        this.WorkingDirectory = directory;
        return this;
    }

    internal void AddArgument(string argument)
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

    public override string ToString()
    {
        var args = CollectArguments();
        return $"{Executable} {args}";
    }
}
