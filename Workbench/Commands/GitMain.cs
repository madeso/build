using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Workbench.Commands.GitCommands;

internal sealed class BlameCommand : Command<BlameCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Git file to blame")]
        [CommandArgument(0, "<git file>")]
        public string File { get; set; } = "";
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        foreach(var line in Git.Blame(new FileInfo(settings.File)))
        {
            AnsiConsole.MarkupLineInterpolated($"{line.Author.Name} {line.Author.Time} {line.FinalLineNumber}: {line.Line}");
        }
        return 0;
    }
}

internal sealed class Status : Command<Status.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Git root to use")]
        [CommandArgument(0, "<git root>")]
        public string Root { get; set; } = "";
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        AnsiConsole.MarkupLineInterpolated($"Status for [green]{settings.Root}[/].");
        var r = Git.Status(settings.Root);
        foreach (var line in r)
        {
            string status = string.Empty;
            if (File.Exists(line.Path))
            {
                status = "file";
            }
            if (Directory.Exists(line.Path))
            {
                status = "dir";
            }

            switch (line.Status)
            {
                case Git.GitStatus.Unknown:
                    AnsiConsole.MarkupLineInterpolated($"Unknown [green]{line.Path}[/] ([blue]{status}[/]).");
                    break;
                case Git.GitStatus.Modified:
                    AnsiConsole.MarkupLineInterpolated($"Modified [blue]{line.Path}[/]  ([blue]{status}[/]).");
                    break;
            }
        }
        return 0;
    }
}

internal sealed class RemoveUnknown : Command<RemoveUnknown.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Git root to use")]
        [CommandArgument(0, "<git root>")]
        public string Root { get; set; } = "";

        [Description("Recursivly remove")]
        [CommandOption("--recursive")]
        [DefaultValue(false)]
        public bool Recursive { get; set; }
    }

    private static void WalkDirectory(string dir, bool recursive)
    {
        AnsiConsole.MarkupLineInterpolated($"Removing unknowns from [green]{dir}[/].");
        var r = Git.Status(dir);
        foreach (var line in r)
        {
            switch (line.Status)
            {
                case Git.GitStatus.Unknown:
                    if (Directory.Exists(line.Path))
                    {
                        AnsiConsole.MarkupLineInterpolated($"Removing directory [blue]{line.Path}[/].");
                        Directory.Delete(line.Path, true);
                    }
                    else
                    {
                        AnsiConsole.MarkupLineInterpolated($"Removing file [red]{line.Path}[/].");
                        File.Delete(line.Path);
                    }
                    break;
                case Git.GitStatus.Modified:
                    if (recursive && Directory.Exists(line.Path))
                    {
                        AnsiConsole.MarkupLineInterpolated($"Modified directory [blue]{line.Path}[/] assumed to be submodule.");
                        WalkDirectory(line.Path, recursive);
                    }
                    break;
            }
        }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        WalkDirectory(settings.Root, settings.Recursive);
        return 0;
    }
}

internal class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, git =>
        {
            git.AddCommand<Status>("status");
            git.AddCommand<BlameCommand>("blame");
            git.AddCommand<RemoveUnknown>("remove-unknown");
        });
    }
}
