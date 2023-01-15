using Spectre.Console.Cli;
using System.Diagnostics.CodeAnalysis;
using Workbench.CompileCommands;

namespace Workbench.Commands.CompileCommandsCommands;


internal sealed class FilesCommand : Command<FilesCommand.Arg>
{
    public sealed class Arg : CommonArguments
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter
            (
                print =>
                {
                    var path = settings.GetPathToCompileCommandsOrNull(print);
                    if (path == null) { return -1; }

                    var commands = Utils.LoadCompileCommandsOrNull(print, path);
                    if (commands == null) { return -1; }

                    print.Info($"{commands}");
                    return 0;
                }
            );
    }
}


internal sealed class IncludesCommand : Command<IncludesCommand.Arg>
{
    public sealed class Arg : CommonArguments
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter
            (
                print =>
                {
                    var path = settings.GetPathToCompileCommandsOrNull(print);
                    if (path == null) { return -1; }

                    var commands = Utils.LoadCompileCommandsOrNull(print, path);
                    if (commands == null) { return -1; }

                    foreach (var (file, command) in commands)
                    {
                        print.Info($"{file}");
                        var dirs = command.GetRelativeIncludes();
                        foreach (var d in dirs)
                        {
                            print.Info($"    {d}");
                        }
                    }
                    return 0;
                }
            );
    }
}

internal sealed class DefinesCommand : Command<DefinesCommand.Arg>
{
    public sealed class Arg : CommonArguments
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter
            (
                print =>
                {
                    var path = settings.GetPathToCompileCommandsOrNull(print);
                    if (path == null) { return -1; }

                    var commands = Utils.LoadCompileCommandsOrNull(print, path);
                    if (commands == null) { return -1; }

                    foreach (var (file, command) in commands)
                    {
                        print.Info($"{file}");
                        var defs = command.GetDefines();
                        foreach (var (k, v) in defs)
                        {
                            print.Info($"    {k} = {v}");
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

