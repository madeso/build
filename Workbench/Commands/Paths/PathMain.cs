using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Spectre.Console;
using Spectre.Console.Cli;
using Workbench.Commands.Clang;
using Workbench.Config;
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
            
            SetupPathCommand.Configure<CompileCommandsArguments>(config, "cc", "var",
                (paths, value) => paths.CompileCommands = value,
                (cc) => CompileCommand.ListAll(cc)
                    .PrintFoundList("compile command", CompileCommand.FindOrNone(cc, null))
                , cc => ToSelectables(CompileCommand.ListAll(cc)));

            var no_extra = Array.Empty<Found<Fil>>();
            AddExecutable(config, p => p.GitExecutable, DefaultExecutables.Git, no_extra);
            AddExecutable(config, p => p.ClangTidyExecutable, DefaultExecutables.ClangTidy, DefaultExecutables.ClangTidyExtra);
            AddExecutable(config, p => p.ClangFormatExecutable, DefaultExecutables.ClangFormat, DefaultExecutables.ClangFormatExtra);
            AddExecutable(config, p => p.GraphvizExecutable, DefaultExecutables.Graphviz, DefaultExecutables.GraphvizExtra);
            AddExecutable(config, p => p.CpplintExecutable, DefaultExecutables.CppLint, no_extra);
        });
    }

    private static void AddExecutable(IConfigurator<CommandSettings> config,
        Func<Config.Paths, Fil?> getter,
        Executable exe, IEnumerable<Found<Fil>> additional)
    {
        SetupPathCommand.Configure<CompileCommandsArguments>(config, exe.Name, "executable",
            (paths, value) => paths.CompileCommands = value,
            _ => list_all_executables()
                .PrintFoundList(exe.ListName, get_executable_or_saved())
            , _ => ToSelectables(list_all_executables()));
        return;

        IEnumerable<Found<Fil>> list_all_executables()
            => Config.Paths.ListAllExecutables(getter, exe);
        Fil? get_executable_or_saved()
            => Config.Paths.GetExecutableOrSaved(null, getter, exe);
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
        Action<Config.Paths, Fil?> setter, Action<TNoArg> list,
        Func<TNoArg, IEnumerable<Fil>> value_getter)
        where TNoArg: CommandSettings
    {
        root.AddBranch(name, branch =>
        {
            branch.SetDescription($"Change the {name} {var_name}");
            branch.AddDelegate<SetVarArg>("set", (_, arg) =>
            {
                return Log.PrintErrorsAtExit(print =>
                {
                    var file = new Fil(arg.Value);
                    if (file.Exists == false)
                    {
                        print.Error($"{file} doesn't exist");
                        return -1;
                    }

                    // todo(Gustav): add additional validations to make sure file is executable

                    var paths = Config.Paths.LoadFromDirectoryOrNull(print);
                    if (paths == null) { return -1; }

                    setter(paths, file);
                    Config.Paths.Save(paths);
                    return 0;
                });
            }).WithDescription($"Set the value of {name}");

            branch.AddDelegate<NoArgs>("clear", (_, _) =>
            {
                return Log.PrintErrorsAtExit(print =>
                {
                    var paths = Config.Paths.LoadFromDirectoryOrNull(print);
                    if (paths == null) { return -1; }
                    setter(paths, null);
                    Config.Paths.Save(paths);
                    return 0;
                });
            }).WithDescription($"Clear the value of {name}");

            branch.AddDelegate<TNoArg>("list", (_, args) =>
            {
                list(args);
                return 0;
            }).WithDescription($"List all values of {name}");

            branch.AddDelegate<TNoArg>("choose", (_, args) =>
            {
                return Log.PrintErrorsAtExit(print =>
                {
                    var values = value_getter(args).ToImmutableArray();
                    if (values.Length <= 1)
                    {
                        print.Error($"Not enough values to choose from, found {values.Length}");
                        return -1;
                    }

                    var paths = Config.Paths.LoadFromDirectoryOrNull(print);
                    if (paths == null) { return -1; }

                    var new_value = AnsiConsole.Prompt(
                        new SelectionPrompt<Fil>()
                            .Title($"Select [green]{name}[/]?")
                            .PageSize(10)
                            .MoreChoicesText("[grey](Move up and down to reveal more choices)[/]")
                            .AddChoices(values));

                    setter(paths, new_value);
                    Config.Paths.Save(paths);
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

