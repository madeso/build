using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Workbench.Commands.Git;

internal sealed class BlameCommand : AsyncCommand<BlameCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Git file to blame")]
        [CommandArgument(0, "<git file>")]
        public string File { get; set; } = "";
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        await foreach(var line in Shared.Git.BlameAsync(new FileInfo(settings.File)))
        {
            AnsiConsole.MarkupLineInterpolated($"{line.Author.Name} {line.Author.Time} {line.FinalLineNumber}: {line.Line}");
        }
        return 0;
    }
}

internal sealed class StatusCommand : AsyncCommand<StatusCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Git root to use")]
        [CommandArgument(0, "<git root>")]
        public string Root { get; set; } = "";
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        AnsiConsole.MarkupLineInterpolated($"Status for [green]{settings.Root}[/].");
        await foreach (var line in Shared.Git.StatusAsync(settings.Root))
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
                case Shared.Git.GitStatus.Unknown:
                    AnsiConsole.MarkupLineInterpolated($"Unknown [green]{line.Path}[/] ([blue]{status}[/]).");
                    break;
                case Shared.Git.GitStatus.Modified:
                    AnsiConsole.MarkupLineInterpolated($"Modified [blue]{line.Path}[/]  ([blue]{status}[/]).");
                    break;
            }
        }
        return 0;
    }
}

internal sealed class RemoveUnknownCommand : AsyncCommand<RemoveUnknownCommand.Arg>
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

    private static async Task WalkDirectoryAsync(string dir, bool recursive)
    {
        AnsiConsole.MarkupLineInterpolated($"Removing unknowns from [green]{dir}[/].");
        await foreach (var line in Shared.Git.StatusAsync(dir))
        {
            switch (line.Status)
            {
                case Shared.Git.GitStatus.Unknown:
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
                case Shared.Git.GitStatus.Modified:
                    if (recursive && Directory.Exists(line.Path))
                    {
                        AnsiConsole.MarkupLineInterpolated($"Modified directory [blue]{line.Path}[/] assumed to be submodule.");
                        await WalkDirectoryAsync(line.Path, recursive);
                    }
                    break;
            }
        }
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        await WalkDirectoryAsync(settings.Root, settings.Recursive);
        return 0;
    }
}

internal sealed class AuthorsCommand : AsyncCommand<AuthorsCommand.Arg>
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

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var authors = new Dictionary<string, State>();
        await foreach (var e in Shared.Git.LogAsync(Environment.CurrentDirectory))
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
