using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Spectre.Console;
using Workbench.Shared;

namespace Workbench.Config;

internal class Paths
{
    [JsonPropertyName("compile_commands")]
    public string? CompileCommands { get; set; }

    public static string GetPath()
    {
        return Path.Join(Environment.CurrentDirectory, FileNames.Paths);
    }

    public static Paths? LoadFromDirectoryOrNull(Log? print)
    {
        return File.Exists(GetPath()) == false
            ? null
            : ConfigFile.LoadOrNull<Paths>(print, GetPath())
            ;
    }

    public static void Save(Paths p)
    {
        ConfigFile.Write(GetPath(), p);
    }
}

internal class SetupPathCommand
{
    internal static void Configure<TNoArg>(IConfigurator<CommandSettings> root, string name,
        Action<Paths, string?> setter, Action<TNoArg> list, Func<TNoArg, IEnumerable<string>> value_getter)
        where TNoArg: CommandSettings
    {
        root.AddBranch(name, branch =>
        {
            branch.SetDescription($"Change the {name} var");
            branch.AddDelegate<SetVarArg>("set", (_, arg) =>
            {
                return Log.PrintErrorsAtExit(print =>
                {
                    var paths = Paths.LoadFromDirectoryOrNull(print);
                    if (paths == null) { return -1; }
                    setter(paths, arg.Value);
                    Paths.Save(paths);
                    return 0;
                });
            }).WithDescription($"Set the value of {name}");

            branch.AddDelegate<NoArgs>("clear", (_, _) =>
            {
                return Log.PrintErrorsAtExit(print =>
                {
                    var paths = Paths.LoadFromDirectoryOrNull(print);
                    if (paths == null) { return -1; }
                    setter(paths, null);
                    Paths.Save(paths);
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
                        print.Error("Not enough values to choose from");
                        return -1;
                    }

                    var paths = Paths.LoadFromDirectoryOrNull(print);
                    if (paths == null) { return -1; }

                    var new_value = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title($"Select [green]{name}[/]?")
                            .PageSize(10)
                            .MoreChoicesText("[grey](Move up and down to reveal more choices)[/]")
                            .AddChoices(values));

                    setter(paths, new_value);
                    Paths.Save(paths);
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

