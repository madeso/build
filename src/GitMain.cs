using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Workbench.Git;

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
        AnsiConsole.MarkupLine($"Status for [green]{settings.Root}[/].");
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
                case GitStatus.Unknown:
                    AnsiConsole.MarkupLine($"Unknown [green]{line.Path}[/] ([blue]{status}[/]).");
                    break;
                case GitStatus.Modified:
                    AnsiConsole.MarkupLine($"Modified [blue]{line.Path}[/]  ([blue]{status}[/]).");
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
        AnsiConsole.MarkupLine($"Removing unknowns from [green]{dir}[/].");
        var r = Git.Status(dir);
        foreach (var line in r)
        {
            switch (line.Status)
            {
                case GitStatus.Unknown:
                    if (Directory.Exists(line.Path))
                    {
                        AnsiConsole.MarkupLine($"Removing directory [blue]{line.Path}[/].");
                        Directory.Delete(line.Path, true);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"Removing file [red]{line.Path}[/].");
                        File.Delete(line.Path);
                    }
                    break;
                case GitStatus.Modified:
                    if (recursive && Directory.Exists(line.Path))
                    {
                        AnsiConsole.MarkupLine($"Modified directory [blue]{line.Path}[/] assumed to be submodule.");
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
            git.AddCommand<RemoveUnknown>("remove-unknown");
        });
    }
}
