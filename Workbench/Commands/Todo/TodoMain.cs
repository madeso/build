using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;
using Workbench.Shared;
using Workbench.Shared.Extensions;

namespace Workbench.Commands.Todo;


internal class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, cmake =>
        {
            cmake.SetDescription("Commands related to comments marked with todo");
            cmake.AddCommand<FindTodosCommand>("find");
            cmake.AddCommand<GroupWithTimeCommand>("with-time");
        });
    }
}


[Description("Recursively list all todo comments in the current folder")]
internal sealed class FindTodosCommand : AsyncCommand<FindTodosCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var cwd = Dir.CurrentDirectory;
        var vread = new ReadFromDisk();

        var cc = new ColCounter<Fil>();
        var log = new LogToConsole();

        var source_files = TodoComments.ListFiles(cwd);
        await SpectreExtensions.Progress().RunArrayAsync(source_files, async file =>
        {
            var todos = await TodoComments.FindTodosInFileAsync(vread, file);

            foreach (var todo in todos)
            {
                log.WriteInformation(new FileLine(todo.File, todo.Line), todo.Todo);
                cc.AddOne(todo.File);
            }

            return file.GetDisplay(cwd);
        });

        {
            var count = cc.TotalCount();
            var files = cc.Keys.Count();
            AnsiConsole.MarkupLineInterpolated($"Found [blue]{count}[/] todos in {files} files");
        }

        AnsiConsole.WriteLine("Top 5 files");
        foreach (var (file, count) in cc.MostCommon().Take(5))
        {
            AnsiConsole.MarkupLineInterpolated($"[blue]{file}[/] with {count} todos");
        }

        return 0;
    }
}



[Description("Find todo comments and group them the last time that line was changed in git")]
internal sealed class GroupWithTimeCommand : AsyncCommand<GroupWithTimeCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var cwd = Dir.CurrentDirectory;
        var paths = new Config.RealPaths();
        var vread = new ReadFromDisk();

        return await CliUtil.PrintErrorsAtExitAsync(async log =>
        {
            var git_path = paths.GetGitExecutable(vread, cwd, log);
            if (git_path == null)
            {
                return -1;
            }

            var todo_list = new List<TodoInFile>();

            var source_files = TodoComments.ListFiles(cwd);
            await SpectreExtensions.Progress().RunArrayAsync(source_files, async file =>
            {
                var todos = await TodoComments.FindTodosInFileAsync(vread, file);
                todo_list.AddRange(todos);
                return file.GetDisplay(cwd);
            });

            // group on file
            var grouped = todo_list.OrderBy(x => x.File.Name)
                .GroupBy(todo => todo.File,
                    (file, todos) => new { File = file, Todos = todos.ToImmutableArray() }
                ).ToImmutableArray();

            // group by file to only run blame once (per file)
            var todo_with_blame = await SpectreExtensions.Progress().MapArrayAsync(grouped, async entry =>
            {
                var blames = await Shared.Git.BlameAsync(cwd, git_path, entry.File).ToListAsync();
                return ($"Blaming {entry.File.GetDisplay(cwd)}", entry.Todos
                        // if there are no blames, this file is probably new and the date is current
                    .Select(x => new { Todo = x, Blame = blames.Count==0 ? DateTime.Now : blame_to_time(blames[x.Line - 1])})
                    .ToImmutableArray());
            });

            // extract from file grouping and order by time
            var sorted_todos = todo_with_blame.SelectMany(x => x)
                .OrderByDescending(x => x.Blame);

            // group on x time ago to break up the info dump
            var time_grouped = sorted_todos.GroupBy(x => x.Blame.GetTimeAgoString(),
                (x, grouped_todos) => new { Time = x, Todos = grouped_todos.ToArray() }
            ).ToArray();

            foreach (var group in time_grouped)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{group.Time}[/]:");
                foreach (var x in group.Todos)
                {
                    var todo = x.Todo;
                    log.WriteInformation(new(todo.File, todo.Line),
                        $"{x.Blame}: {todo.Todo}");
                }
            }

            return 0;
        });

        // use author datetime when sorting
        static DateTime blame_to_time(Shared.Git.BlameLine b) => b.Author.Time;
    }
}
