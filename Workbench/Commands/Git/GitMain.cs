using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices.Marshalling;
using Spectre.Console;
using Spectre.Console.Cli;
using Workbench.Shared;
using Workbench.Shared.Extensions;

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
        return await Log.PrintErrorsAtExitAsync(async log =>
        {
            var git_path = Config.Paths.GetGitExecutable(log);
            if (git_path == null)
            {
                return -1;
            }

            await foreach (var line in Shared.Git.BlameAsync(git_path, new Fil(settings.File)))
            {
                AnsiConsole.MarkupLineInterpolated(
                    $"{line.Author.Name} {line.Author.Time} {line.FinalLineNumber}: {line.Line}");
            }

            return 0;
        });
    }
}

internal sealed class StatusCommand : AsyncCommand<StatusCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Git root to use")]
        [CommandArgument(0, "[git root]")]
        public string Root { get; set; } = "";

        [Description("Show metrics instead of files")]
        [CommandOption("--metrics")]
        [DefaultValue(false)]
        public bool ShowMetrics { get; set; } = false;
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return await Log.PrintErrorsAtExitAsync(async log =>
        {
            var git_path = Config.Paths.GetGitExecutable(log);
            if (git_path == null)
            {
                return -1;
            }

            var root = Cli.RequireDirectory(log, settings.Root, "git root");
            if (root == null)
            {
                return -1;
            }

            var unknowns = new ColCounter<string>();
            var modified = new ColCounter<string>();
            var added = new ColCounter<string>();

            AnsiConsole.MarkupLineInterpolated($"Status for [green]{settings.Root}[/].");
            await foreach (var line in Shared.Git.StatusAsync(git_path, root))
            {
                string io_type = string.Empty;

                if (line.Path.IsFile)
                {
                    io_type = "file";

                    var x = line.Path.AsFile?.Extension ?? "";
                    switch(line.Status)
                    {
                        case Shared.Git.GitStatus.Unknown:
                            unknowns.AddOne(x);
                            break;
                        case Shared.Git.GitStatus.Modified:
                            modified.AddOne(x);
                            break;
                        case Shared.Git.GitStatus.Added:
                            added.AddOne(x);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"status not handled {line.Status}");
                    }
                }

                if (line.Path.IsDirectory)
                {
                    io_type = "dir";
                }

                if(settings.ShowMetrics == false)
                {
                    switch (line.Status)
                    {
                        case Shared.Git.GitStatus.Unknown:
                            AnsiConsole.MarkupLineInterpolated($"Unknown [green]{line.Path}[/] ([blue]{io_type}[/]).");
                            break;
                        case Shared.Git.GitStatus.Modified:
                            AnsiConsole.MarkupLineInterpolated($"Modified [blue]{line.Path}[/]  ([blue]{io_type}[/]).");
                            break;
                        case Shared.Git.GitStatus.Added:
                            AnsiConsole.MarkupLineInterpolated($"Added [blue]{line.Path}[/]  ([blue]{io_type}[/]).");
                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"status not handled {line.Status}");
                    }
                }
            }

            if(settings.ShowMetrics)
            {
                print("unknowns", unknowns);
                print("modified", modified);
                print("added", added);
            }

            return 0;

            static void print(string title, ColCounter<string> counts)
            {
                if (!counts.Keys.Any())
                {
                    return;
                }

                int max = 10;
                AnsiConsole.MarkupLineInterpolated($"{title}:");
                foreach (var (what, count) in counts.MostCommon().Take(max))
                {
                    AnsiConsole.MarkupLineInterpolated($"- [GREEN]{what}[/] ([BLUE]{count}[/])");
                }
                AnsiConsole.WriteLine();
            }
        });
    }
}

internal sealed class RemoveUnknownCommand : AsyncCommand<RemoveUnknownCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Git root to use")]
        [CommandArgument(0, "[git root]")]
        public string Root { get; set; } = "";

        [Description("Recursivly remove")]
        [CommandOption("--recursive")]
        [DefaultValue(false)]
        public bool Recursive { get; set; }
    }

    private static async Task WalkDirectoryAsync(Fil git_path, Dir dir, bool recursive)
    {
        AnsiConsole.MarkupLineInterpolated($"Removing unknowns from [green]{dir}[/].");
        await foreach (var line in Shared.Git.StatusAsync(git_path, dir))
        {
            switch (line.Status)
            {
                case Shared.Git.GitStatus.Unknown:
                    if (line.Path.IsDirectory)
                    {
                        AnsiConsole.MarkupLineInterpolated($"Removing directory [blue]{line.Path}[/].");
                        Directory.Delete(line.Path.Path, true);
                    }
                    else
                    {
                        AnsiConsole.MarkupLineInterpolated($"Removing file [red]{line.Path}[/].");
                        File.Delete(line.Path.Path);
                    }
                    break;
                case Shared.Git.GitStatus.Modified:
                    if (recursive && line.Path is { IsDirectory: true, Exists: true })
                    {
                        AnsiConsole.MarkupLineInterpolated($"Modified directory [blue]{line.Path}[/] assumed to be submodule.");
                        await WalkDirectoryAsync(git_path, line.Path.AsDir!, recursive);
                    }
                    break;
            }
        }
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return await Log.PrintErrorsAtExitAsync(async log =>
        {
            var git_path = Config.Paths.GetGitExecutable(log);
            if (git_path == null)
            {
                return -1;
            }

            var root = Cli.RequireDirectory(log, settings.Root, "root");
            if (root == null)
            {
                return -1;
            }

            await WalkDirectoryAsync(git_path, root, settings.Recursive);
            return 0;
        });
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
        return await Log.PrintErrorsAtExitAsync(async log =>
        {
            var git_path = Config.Paths.GetGitExecutable(log);
            if (git_path == null)
            {
                return -1;
            }

            var authors = new Dictionary<string, State>();
            var cwd = Dir.CurrentDirectory;
            await foreach (var e in Shared.Git.LogAsync(git_path, cwd))
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

            foreach (var entry in authors.Values.OrderBy(e => e.Start))
            {
                var total = entry.End.Subtract(entry.Start);
                AnsiConsole.MarkupLineInterpolated(
                    $"[blue]{entry.Email}[/]: {entry.Start} - {entry.End}: [red]{total}[/]");
            }

            return 0;
        });
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
