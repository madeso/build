using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;
using Workbench.Utils;

namespace Workbench.Commands.CompileCommands;


internal sealed class FilesCommand : Command<FilesCommand.Arg>
{
    public sealed class Arg : CompileCommandsArguments
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return Printer.PrintErrorsAtExit
            (
                print =>
                {
                    var path = settings.GetPathToCompileCommandsOrNull(print);
                    if (path == null) { return -1; }

                    var commands = CompileCommand.LoadCompileCommandsOrNull(print, path);
                    if (commands == null) { return -1; }

                    AnsiConsole.WriteLine($"{commands}");
                    return 0;
                }
            );
    }
}


internal sealed class IncludesCommand : Command<IncludesCommand.Arg>
{
    public sealed class Arg : CompileCommandsArguments
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return Printer.PrintErrorsAtExit
            (
                print =>
                {
                    var path = settings.GetPathToCompileCommandsOrNull(print);
                    if (path == null) { return -1; }

                    var commands = CompileCommand.LoadCompileCommandsOrNull(print, path);
                    if (commands == null) { return -1; }

                    foreach (var (file, command) in commands)
                    {
                        AnsiConsole.WriteLine($"{file}");
                        var dirs = command.GetRelativeIncludes();
                        foreach (var d in dirs)
                        {
                            AnsiConsole.WriteLine($"    {d}");
                        }
                    }
                    return 0;
                }
            );
    }
}

internal sealed class DefinesCommand : Command<DefinesCommand.Arg>
{
    public sealed class Arg : CompileCommandsArguments
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return Printer.PrintErrorsAtExit
            (
                print =>
                {
                    var path = settings.GetPathToCompileCommandsOrNull(print);
                    if (path == null) { return -1; }

                    var commands = CompileCommand.LoadCompileCommandsOrNull(print, path);
                    if (commands == null) { return -1; }

                    foreach (var (file, command) in commands)
                    {
                        AnsiConsole.WriteLine($"{file}");
                        var defines = command.GetDefines();
                        foreach (var (k, v) in defines)
                        {
                            AnsiConsole.WriteLine($"    {k} = {v}");
                        }
                    }
                    return 0;
                }
            );
    }
}

internal static class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, cmake =>
        {
            cmake.SetDescription("Tool to list headers");
            cmake.AddCommand<FilesCommand>("files").WithDescription("list all files in the compile commands class");
            cmake.AddCommand<IncludesCommand>("includes").WithDescription("list include directories per file");
            cmake.AddCommand<DefinesCommand>("defines").WithDescription("list include directories per file");
        });
    }
}

