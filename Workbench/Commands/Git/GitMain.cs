using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Workbench.Commands.Git;

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
        foreach(var line in Workbench.Git.Blame(new FileInfo(settings.File)))
        {
            AnsiConsole.MarkupLineInterpolated($"{line.Author.Name} {line.Author.Time} {line.FinalLineNumber}: {line.Line}");
        }
        return 0;
    }
}

internal sealed class StatusCommand : Command<StatusCommand.Arg>
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
        var r = Workbench.Git.Status(settings.Root);
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
                case Workbench.Git.GitStatus.Unknown:
                    AnsiConsole.MarkupLineInterpolated($"Unknown [green]{line.Path}[/] ([blue]{status}[/]).");
                    break;
                case Workbench.Git.GitStatus.Modified:
                    AnsiConsole.MarkupLineInterpolated($"Modified [blue]{line.Path}[/]  ([blue]{status}[/]).");
                    break;
            }
        }
        return 0;
    }
}

internal sealed class RemoveUnknownCommand : Command<RemoveUnknownCommand.Arg>
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
        var r = Workbench.Git.Status(dir);
        foreach (var line in r)
        {
            switch (line.Status)
            {
                case Workbench.Git.GitStatus.Unknown:
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
                case Workbench.Git.GitStatus.Modified:
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

internal sealed class AuthorsCommand : Command<AuthorsCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
    }

    class State
    {
        public State(string email, DateTime date)
        {
            Email = email;
            Start = date;
            End = date;
        }

        public DateTime End { get; private set; }

        public DateTime Start { get; private set; }

        public string Email { get; }

        public void Expand(DateTime d)
        {
            if (d < Start)
            {
                Start = d;
            }

            if (d > End)
            {
                End = d;
            }
        }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var authors = new Dictionary<string, State>();
        foreach (var e in Workbench.Git.Log(Environment.CurrentDirectory))
        {
            var email = e.AuthorEmail;
            var date = e.AuthorDate;

            if (false == authors.TryGetValue(email, out var s))
            {
                s = new State(email, date);
                authors.Add(email, s);
            }

            s.Expand(date);
        }
        foreach (var entry in authors.Values.OrderBy(e=>e.Start))
        {
            var total = entry.End.Subtract(entry.Start);
            AnsiConsole.MarkupLineInterpolated($"[blue]{entry.Email}[/]: {entry.Start} - {entry.End}: [red]{total}[/]");
        }
        return 0;
    }
}

internal class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, git =>
        {
            git.AddCommand<StatusCommand>("status");
            git.AddCommand<BlameCommand>("blame");
            git.AddCommand<RemoveUnknownCommand>("remove-unknown");
            git.AddCommand<AuthorsCommand>("authors");
        });
    }
}
