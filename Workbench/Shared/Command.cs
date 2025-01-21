using System.Collections.Immutable;
using Spectre.Console;

namespace Workbench.Shared;

using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

public record ProcessExit(string CommandLine, int ExitCode);
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

public interface Executor
{
    Task<ProcessExit> RunWithCallbackAsync(ImmutableArray<string> arguments, Fil exe, Dir cwd, IEnumerable<string>? input, Action<string> on_stdout, Action<string> on_stderr, Action<string, Exception> on_fail);
}

public static class ExecHelper
{
    public static string CollapseArgStringToSingle(IEnumerable<string> args, PlatformID platform)
    {
        // https://stackoverflow.com/a/10489920
        var result = "";

        if (platform is PlatformID.Unix
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
            foreach (var arg in args)
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

    public static string ToCommandlineDescription(PlatformID platform, Fil exec, IEnumerable<string> arguments)
    {
        var args = CollapseArgStringToSingle(arguments, platform);
        return $"{exec} {args}";
    }
}

public class SystemExecutor : Executor
{
    public async Task<ProcessExit> RunWithCallbackAsync(ImmutableArray<string> arguments, Fil exe, Dir cwd, IEnumerable<string>? input, Action<string> on_stdout, Action<string> on_stderr, Action<string, Exception> on_fail)
    {
        var commandline_display = ExecHelper.ToCommandlineDescription(Environment.OSVersion.Platform, exe, arguments);

        // Prepare the process to run
        ProcessStartInfo start = new()
        {
            Arguments = ExecHelper.CollapseArgStringToSingle(arguments, Environment.OSVersion.Platform),
            FileName = exe.Path,
            UseShellExecute = false,

            WorkingDirectory = cwd.Path,

            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        if (input != null)
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
                foreach (var line in input)
                {
                    await proc.StandardInput.WriteLineAsync(line);
                }
                proc.StandardInput.Close();
            }

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync();

            return new(commandline_display, proc.ExitCode);
        }
        catch (Win32Exception err)
        {
            on_fail($"Failed to run {commandline_display}", err);
            return new(commandline_display, -42);
        }
    }
}

public class ProcessBuilder
{
    internal async Task<ProcessExit> RunWithCallbackAsync(Executor exec,
        Dir cwd, IEnumerable<string>? input, Action<string> on_stdout, Action<string> on_stderr, Action<string, Exception> on_fail)
    {
        return await exec.RunWithCallbackAsync([..arguments], Executable, (WorkingDirectory ?? cwd), input, on_stdout, on_stderr, on_fail);
    }

    internal async Task<ProcessExitWithOutput> RunAndGetOutputAsync(Executor exec, Dir cwd)
    {
        var output = new List<OutputLine>();

        var ret = await RunWithCallbackAsync(exec, cwd, null,
            line => output.Add(new OutputLine(line, false)),
            line => output.Add(new OutputLine(line, true)),
            (line, ex) => {
                output.Add(new OutputLine(line, true));
                output.Add(new OutputLine(ex.Message, true));
            });

        return new(ret, output.ToArray());
    }

    internal async Task<ProcessExitWithOutput> RunAndGetOutputAsync(Executor exec, Dir cwd, IEnumerable<string> lines)
    {
        var output = new List<OutputLine>();

        var ret = await RunWithCallbackAsync(exec, cwd, lines,
            line => output.Add(new OutputLine(line, false)),
            line => output.Add(new OutputLine(line, true)),
            (line, ex) => {
                output.Add(new OutputLine(line, true));
                output.Add(new OutputLine(ex.Message, true));
            });

        return new(ret, output.ToArray());
    }

    private Fil Executable { get; }
    private readonly List<string> arguments = new();
    public Dir? WorkingDirectory { get; set; } = null;

    public ProcessBuilder(Fil executable, params string[] arguments)
    {
        Executable = executable;
        foreach (var arg in arguments)
        {
            AddArgument(arg);
        }
    }

    public ProcessBuilder InDirectory(Dir directory)
    {
        WorkingDirectory = directory;
        return this;
    }

    internal void AddArgument(string argument)
    {
        arguments.Add(argument);
    }

    internal async Task RunAndPrintOutputAsync(Executor exec, Dir cwd, Log log)
    {
        var pe = await RunWithCallbackAsync(exec, cwd, null, AnsiConsole.WriteLine, log.Warning,
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
