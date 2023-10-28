﻿using System.Collections.Immutable;
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
            
            SetupPathCommand.Configure<CompileCommandsArguments>(config, "cc",
                (paths, value) => paths.CompileCommands = value,
                (cc) => CompileCommand.ListAll(cc)
                    .PrintFoundList("compile command", CompileCommand.FindOrNone(cc, null))
                , cc => ToSelectables(CompileCommand.ListAll(cc)));

            AddExecutable(config, p => p.GitExecutable, DefaultExecutables.Git);
            AddExecutable(config, p => p.ClangTidyExecutable, DefaultExecutables.ClangTidy);
            AddExecutable(config, p => p.ClangFormatExecutable, DefaultExecutables.ClangFormat);

        });
    }

    private static void AddExecutable(IConfigurator<CommandSettings> config,
        Func<Config.Paths, string?> getter,
        Executable exe)
    {
        SetupPathCommand.Configure<CompileCommandsArguments>(config, exe.Name,
            (paths, value) => paths.CompileCommands = value,
            _ => list_all_executables()
                .PrintFoundList(exe.ListName, get_executable_or_saved())
            , _ => ToSelectables(list_all_executables()));
        return;

        IEnumerable<Found<string>> list_all_executables()
            => Config.Paths.ListAllExecutables(getter, exe);
        string? get_executable_or_saved()
            => Config.Paths.GetExecutableOrSaved(null, getter, exe);
    }

    private static IEnumerable<string> ToSelectables(IEnumerable<Found<string>> founds)
        => founds.SelectMany(x => x.Findings)
            .Select(v => v.ValueOrNull)
            .IgnoreNull()
            .Distinct();
}


internal class SetupPathCommand
{
    internal static void Configure<TNoArg>(IConfigurator<CommandSettings> root, string name,
        Action<Config.Paths, string?> setter, Action<TNoArg> list,
        Func<TNoArg, IEnumerable<string>> value_getter)
        where TNoArg: CommandSettings
    {
        root.AddBranch(name, branch =>
        {
            branch.SetDescription($"Change the {name} var");
            branch.AddDelegate<SetVarArg>("set", (_, arg) =>
            {
                return Log.PrintErrorsAtExit(print =>
                {
                    if (File.Exists(arg.Value) == false)
                    {
                        AnsiConsole.WriteLine($"ERROR: {arg.Value} doesn't exist");
                        return -1;
                    }
                    var paths = Config.Paths.LoadFromDirectoryOrNull(print);
                    if (paths == null) { return -1; }
                    setter(paths, arg.Value);
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
                        new SelectionPrompt<string>()
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

