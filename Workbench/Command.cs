namespace Workbench;

using System.Diagnostics;

internal record ProcessExit(string CommandLine, int ExitCode);

internal class ProcessExitWithOutput
{
    public string CommandLine { get; private init; }
    public int ExitCode { get; private init; }
    public string[] Output { get; private init; }

    public ProcessExitWithOutput(ProcessExit pe, string[] output)
    {
        CommandLine = pe.CommandLine;
        ExitCode = pe.ExitCode;
        Output = output;
    }

    public string[] RequireSuccess()
    {
        if (ExitCode != 0)
        {
            var outputString = string.Join('\n', Output);
            throw new Exception($"{CommandLine} has exit code {ExitCode}:\n{outputString}");
        }

        return Output;
    }

    public ProcessExitWithOutput PrintOutput(Printer print)
    {
        foreach (var line in Output)
        {
            print.Info(line);
        }

        return this;
    }

    public ProcessExitWithOutput PrintStatus(Printer print)
    {
        print.Info($"Return value: {ExitCode}");
        if (ExitCode != 0)
        {
            print.error($"Failed to run command: {CommandLine}");
        }

        return this;
    }

    public ProcessExitWithOutput PrintStatusAndUpdate(Printer print)
    {
        PrintStatus(print);
        PrintOutput(print);
        return this;
    }
}

public class ProcessBuilder
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

        return new(ToString(), proc.ExitCode);
    }

    internal ProcessExitWithOutput RunAndGetOutput()
    {
        var output = new List<string>();

        var ret = RunWithCallback(line => output.Add(line));

        return new(ret, output.ToArray());
    }

    private string Executable { get; }
    private readonly List<string> arguments = new();
    public string? WorkingDirectory { get; set; } = "";

    public ProcessBuilder(string executable, params string[] arguments)
    {
        Executable = executable;
        foreach (var arg in arguments)
        {
            AddArgument(arg);
        }
    }

    public ProcessBuilder InDirectory(string directory)
    {
        WorkingDirectory = directory;
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

        if (Environment.OSVersion.Platform is PlatformID.Unix
            or
            PlatformID.MacOSX)
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
