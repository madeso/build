using System.Collections.Immutable;
using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Workbench.Shared;
using Workbench.Shared.Extensions;

namespace Workbench.Commands.Paths;

public class Main
{
    internal static void Configure(IConfigurator root, string name)
    {
        root.AddBranch(name, config =>
        {
            config.SetDescription("Get or set custom paths");

            var vfs = new VfsDisk();
            
            SetupPathCommand.Configure<CompileCommandsArguments>(config, "compile-command", "file",
                (paths, value) => paths.CompileCommands = value,
                (cc, cwd, paths) => CompileCommand.ListAll(vfs, cwd, cc, paths)
                    .PrintFoundList("compile command", CompileCommand.FindOrNone(vfs, cwd, cc, null, paths))
                , (cc, cwd, paths) => ToSelectables(CompileCommand.ListAll(vfs, cwd, cc, paths)));

            var no_extra = Array.Empty<Found<Fil>>();
            AddExecutable(vfs, config, p => p.GitExecutable, (p,v) => p.GitExecutable = v, DefaultExecutables.Git);
            AddExecutable(vfs, config, p => p.ClangTidyExecutable, (p,v) => p.ClangTidyExecutable = v, DefaultExecutables.ClangTidy);
            AddExecutable(vfs, config, p => p.ClangFormatExecutable, (p,v) => p.ClangFormatExecutable = v, DefaultExecutables.ClangFormat);
            AddExecutable(vfs, config, p => p.GraphvizExecutable, (p,v) => p.GraphvizExecutable = v, DefaultExecutables.Graphviz);
            AddExecutable(vfs, config, p => p.CpplintExecutable, (p,v) => p.CpplintExecutable = v, DefaultExecutables.CppLint);
        });
    }

    private static void AddExecutable(Vfs vfs, IConfigurator<CommandSettings> config,
        Func<Config.SavedPaths, Fil?> getter,
        Action<Config.SavedPaths, Fil?> setter,
        Executable exe)
    {
        SetupPathCommand.Configure<CompileCommandsArguments>(config, exe.Name, "executable",
            setter,
            (_, cwd, paths) => list_all_executables(paths, cwd)
                .PrintFoundList(exe.ListName, get_executable_or_saved(paths, cwd))
            , (_, cwd, paths) => ToSelectables(list_all_executables(paths, cwd)));
        return;

        IEnumerable<Found<Fil>> list_all_executables(Config.Paths paths, Dir cwd)
            => paths.ListAllExecutables(vfs, cwd, getter, exe);
        Fil? get_executable_or_saved(Config.Paths paths, Dir cwd)
            => paths.GetSavedOrSearchForExecutable(vfs, cwd, null, getter, exe);
    }

    private static IEnumerable<Fil> ToSelectables(IEnumerable<Found<Fil>> founds)
        => founds.SelectMany(x => x.Findings)
            .Select(v => v.ValueOrNull)
            .IgnoreNull()
            .Distinct();
}


internal class SetupPathCommand
{
    internal static void Configure<TNoArg>(IConfigurator<CommandSettings> root, string name, string var_name,
        Action<Config.SavedPaths, Fil?> setter,
        Action<TNoArg, Dir, Config.Paths> list,
        Func<TNoArg, Dir, Config.Paths, IEnumerable<Fil>> value_getter)
        where TNoArg: CommandSettings
    {
        root.AddBranch(name, branch =>
        {
            branch.SetDescription($"Change the {name} {var_name}");
            branch.AddDelegate<SetVarArg>("set", (_, arg) =>
            {
                var cwd = Dir.CurrentDirectory;
                var paths = new Config.RealPaths();
                var vfs = new VfsDisk();

                return CliUtil.PrintErrorsAtExit(print =>
                {
                    var file = new Fil(arg.Value);

                    if (file.Exists == false)
                    {
                        print.Error($"{file} doesn't exist");
                        return -1;
                    }

                    // todo(Gustav): add additional validations to make sure file is executable

                    var saved_paths = paths.LoadConfigFromCurrentDirectoryOrNull(vfs, cwd, print);
                    if (saved_paths == null) { return -1; }

                    setter(saved_paths, file);
                    paths.Save(vfs, cwd, saved_paths);
                    return 0;
                });
            }).WithDescription($"Set the value of {name}");

            branch.AddDelegate<NoArgs>("clear", (_, _) =>
            {
                var cwd = Dir.CurrentDirectory;
                var paths = new Config.RealPaths();
                var vfs = new VfsDisk();

                return CliUtil.PrintErrorsAtExit(print =>
                {
                    var saved_paths = paths.LoadConfigFromCurrentDirectoryOrNull(vfs, cwd, print);
                    if (saved_paths == null) { return -1; }
                    setter(saved_paths, null);
                    paths.Save(vfs, cwd, saved_paths);
                    return 0;
                });
            }).WithDescription($"Clear the value of {name}");

            branch.AddDelegate<TNoArg>("list", (_, args) =>
            {
                var cwd = Dir.CurrentDirectory;
                var paths = new Config.RealPaths();

                list(args, cwd, paths);
                return 0;
            }).WithDescription($"List all values of {name}");

            branch.AddDelegate<TNoArg>("choose", (_, args) =>
            {
                var cwd = Dir.CurrentDirectory;
                var paths = new Config.RealPaths();
                var vfs = new VfsDisk();

                return CliUtil.PrintErrorsAtExit(print =>
                {
                    var values = value_getter(args, cwd, paths).ToImmutableArray();
                    if (values.Length <= 1)
                    {
                        print.Error($"Not enough values to choose from, found {values.Length}");
                        return -1;
                    }

                    var saved_paths = paths.LoadConfigFromCurrentDirectoryOrNull(vfs, cwd, print);
                    if (saved_paths == null) { return -1; }

                    var new_value = AnsiConsole.Prompt(
                        new SelectionPrompt<Fil>()
                            .Title($"Select [green]{name}[/]?")
                            .PageSize(10)
                            .MoreChoicesText("[grey](Move up and down to reveal more choices)[/]")
                            .AddChoices(values));

                    setter(saved_paths, new_value);
                    paths.Save(vfs, cwd, saved_paths);
                    AnsiConsole.WriteLine($"{name} changed to {new_value}");
                    return 0;
                });
            }).WithDescription($"List all values of {name}");
        });
    }
}

public sealed class SetVarArg : CommandSettings
{
    [Description("New value")]
    [CommandArgument(0, "<value>")]
    public string Value { get; set; } = string.Empty;
}

public sealed class NoArgs : CommandSettings
{
}

