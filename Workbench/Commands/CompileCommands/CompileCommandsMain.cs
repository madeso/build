using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;
using Workbench.Shared;

namespace Workbench.Commands.CompileCommands;


internal sealed class FilesCommand : Command<FilesCommand.Arg>
{
    public sealed class Arg : CompileCommandsArguments
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var cwd = Dir.CurrentDirectory;
        var paths = new Config.RealPaths();
        var vfs = new VfsDisk();

        return CliUtil.PrintErrorsAtExit
            (
                print =>
                {
                    var path = CompileCommand.FindOrNone(vfs, cwd, settings, print, paths);
                    if (path == null) { return -1; }

                    var commands = CompileCommand.LoadCompileCommandsOrNull(vfs, print, path);
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
        var cwd = Dir.CurrentDirectory;
        var paths = new Config.RealPaths();
        var vfs = new VfsDisk();

        return CliUtil.PrintErrorsAtExit
            (
                print =>
                {
                    var path = CompileCommand.FindOrNone(vfs, cwd, settings, print, paths);
                    if (path == null) { return -1; }

                    var commands = CompileCommand.LoadCompileCommandsOrNull(vfs, print, path);
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
        var cwd = Dir.CurrentDirectory;
        var paths = new Config.RealPaths();
        var vfs = new VfsDisk();

        return CliUtil.PrintErrorsAtExit
            (
                print =>
                {
                    var path = CompileCommand.FindOrNone(vfs, cwd, settings, print, paths);
                    if (path == null) { return -1; }

                    var commands = CompileCommand.LoadCompileCommandsOrNull(vfs, print, path);
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

