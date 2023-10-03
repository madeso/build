namespace Workbench.Utils;

using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using Workbench;

internal record ProcessExit(string CommandLine, int ExitCode);

internal record OutputLine(string Line, bool IsError);

internal class ProcessExitWithOutput
{
    public string CommandLine { get; private init; }
    public int ExitCode { get; private init; }
    public OutputLine[] Output { get; private init; }

    public ProcessExitWithOutput(ProcessExit pe, OutputLine[] output)
    {
        CommandLine = pe.CommandLine;
        ExitCode = pe.ExitCode;
        Output = output;
    }

    public OutputLine[] RequireSuccess()
    {
        if (ExitCode != 0)
        {
            var outputString = string.Join('\n', Output.Select(x => x.Line));
            throw new Exception($"{CommandLine} has exit code {ExitCode}:\n{outputString}");
        }

        return Output;
    }

    public ProcessExitWithOutput PrintOutput(Printer print)
    {
        foreach (var line in Output)
        {
            if(line.IsError)
            {
                print.Error(line.Line);
            }
            else
            {
                print.Info(line.Line);
            }
        }

        return this;
    }

    public ProcessExitWithOutput PrintStatus(Printer print)
    {
        print.Info($"Return value: {ExitCode}");
        if (ExitCode != 0)
        {
            print.Error($"Failed to run command: {CommandLine}");
        }

        return this;
    }

    public ProcessExitWithOutput PrintStatusAndOuput(Printer print)
    {
        PrintStatus(print);
        PrintOutput(print);
        return this;
    }
}

public class ProcessBuilder
{
    internal ProcessExit RunWithCallback(IEnumerable<string>? input, Action<string> onStdout, Action<string> onStdErr, Action<string, Exception> onFail)
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

        if(input != null)
        {
            start.RedirectStandardInput = true;
        }

        var proc = new Process { StartInfo = start };

        proc.OutputDataReceived += (sender, e) => { if (e.Data != null) { onStdout(e.Data); } };
        proc.ErrorDataReceived += (sender, e) => { if (e.Data != null) { onStdErr(e.Data); } };

        try
        {
            proc.Start();

            if (input != null)
            {
                foreach(var line in input)
                {
                    proc.StandardInput.WriteLine(line);
                }
                proc.StandardInput.Close();
            }

            
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.WaitForExit();

            return new(ToString(), proc.ExitCode);
        }
        catch (Win32Exception err)
        {
            onFail($"Failed to run {ToString()}", err);
            return new(ToString(), -42);
        }
    }

    internal ProcessExitWithOutput RunAndGetOutput()
    {
        var output = new List<OutputLine>();

        var ret = RunWithCallback(null,
            line => output.Add(new OutputLine(line, false)),
            line => output.Add(new OutputLine(line, true)),
            (line, ex) => {
                output.Add(new OutputLine(line, true));
                output.Add(new OutputLine(ex.Message, true));
            });

        return new(ret, output.ToArray());
    }

    internal ProcessExitWithOutput RunAndGetOutput(IEnumerable<string> lines)
    {
        var output = new List<OutputLine>();

        var ret = RunWithCallback(lines,
            line => output.Add(new OutputLine(line, false)),
            line => output.Add(new OutputLine(line, true)),
            (line, ex) => {
                output.Add(new OutputLine(line, true));
                output.Add(new OutputLine(ex.Message, true));
            });

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

    internal void RunAndPrintOutput(Printer printer)
    {
        printer.PrintStatus(RunWithCallback(null, printer.Info, err => printer.Error(err),
            (mess, ex) => {
                    printer.Error(mess);
                    printer.Error(ex.Message);
                }
            ));
    }
}
