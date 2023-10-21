using Spectre.Console;

namespace Workbench.Shared;

using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

internal record ProcessExit(string CommandLine, int ExitCode);
internal record OutputLine(string Line, bool IsError);

internal class ProcessExitWithOutput
{
    public string CommandLine { get; }
    public int ExitCode { get; }
    public OutputLine[] Output { get; }

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
            var output_string = string.Join('\n', Output.Select(x => x.Line));
            throw new Exception($"{CommandLine} has exit code {ExitCode}:\n{output_string}");
        }

        return Output;
    }

    public ProcessExitWithOutput PrintOutput(Log print)
    {
        foreach (var line in Output)
        {
            if(line.IsError)
            {
                print.Error(line.Line);
            }
            else
            {
                AnsiConsole.WriteLine(line.Line);
            }
        }

        return this;
    }
}

public class ProcessBuilder
{
    internal async Task<ProcessExit> RunWithCallbackAsync(
        IEnumerable<string>? input, Action<string> on_stdout, Action<string> on_stderr, Action<string, Exception> on_fail)
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

        proc.OutputDataReceived += (_, e) => { if (e.Data != null) { on_stdout(e.Data); } };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) { on_stderr(e.Data); } };

        try
        {
            proc.Start();

            if (input != null)
            {
                foreach(var line in input)
                {
                    await proc.StandardInput.WriteLineAsync(line);
                }
                proc.StandardInput.Close();
            }
            
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync();

            return new(ToString(), proc.ExitCode);
        }
        catch (Win32Exception err)
        {
            on_fail($"Failed to run {ToString()}", err);
            return new(ToString(), -42);
        }
    }

    internal async Task<ProcessExitWithOutput> RunAndGetOutputAsync()
    {
        var output = new List<OutputLine>();

        var ret = await RunWithCallbackAsync(null,
            line => output.Add(new OutputLine(line, false)),
            line => output.Add(new OutputLine(line, true)),
            (line, ex) => {
                output.Add(new OutputLine(line, true));
                output.Add(new OutputLine(ex.Message, true));
            });

        return new(ret, output.ToArray());
    }

    internal async Task<ProcessExitWithOutput> RunAndGetOutputAsync(IEnumerable<string> lines)
    {
        var output = new List<OutputLine>();

        var ret = await RunWithCallbackAsync(lines,
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
        var result = "";

        if (Environment.OSVersion.Platform is PlatformID.Unix
            or
            PlatformID.MacOSX)
        {
            foreach (var arg in args)
            {
                result += (result.Length > 0 ? " " : "")
                    + arg
                        .Replace(" ", @"\ ")
                        .Replace("\t", "\\\t")
                        .Replace(@"\", @"\\")
                        .Replace(@"""", @"\""")
                        .Replace("<", @"\<")
                        .Replace(">", @"\>")
                        .Replace("|", @"\|")
                        .Replace("@", @"\@")
                        .Replace("&", @"\&");
            }
        }
        else //Windows family
        {
            foreach (string arg in args)
            {
                var enclosed_in_apo = arg.LastIndexOfAny(
                    new[] { ' ', '\t', '|', '@', '^', '<', '>', '&' }) >= 0;
                var was_apo = enclosed_in_apo;
                var sub_result = "";
                for (int i = arg.Length - 1; i >= 0; i--)
                {
                    switch (arg[i])
                    {
                        case '"':
                            sub_result = @"\""" + sub_result;
                            was_apo = true;
                            break;
                        case '\\':
                            sub_result = (was_apo ? @"\\" : @"\") + sub_result;
                            break;
                        default:
                            sub_result = arg[i] + sub_result;
                            was_apo = false;
                            break;
                    }
                }
                result += (result.Length > 0 ? " " : "")
                    + (enclosed_in_apo ? "\"" + sub_result + "\"" : sub_result);
            }
        }

        return result;
    }

    public override string ToString()
    {
        var args = CollectArguments();
        return $"{Executable} {args}";
    }

    internal async Task RunAndPrintOutputAsync(Log log)
    {
        var pe = await RunWithCallbackAsync(null, AnsiConsole.WriteLine, log.Error,
            (mess, ex) => {
                log.Error(mess);
                log.Error(ex.Message);
            }
        );
        var message = $"{pe.CommandLine} exited with {pe.ExitCode}";
        if(pe.ExitCode == 0)
        {
            AnsiConsole.WriteLine(message);
        }
        else
        {
            log.Error(message);
        }
    }
}
