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
            cmake.SetDescription("todo-comments related commands");
            cmake.AddCommand<FindTodosCommand>("find");
            cmake.AddCommand<GroupWithTimeCommand>("with-time");
        });
    }
}


[Description("list line counts")]
internal sealed class FindTodosCommand : AsyncCommand<FindTodosCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var root = new DirectoryInfo(Environment.CurrentDirectory);

        var cc = new ColCounter<string>();

        var source_files = TodoComments.ListFiles(root);
        await TodoComments.Progress().RunArrayAsync(source_files, async file =>
        {
            var todos = await TodoComments.FindTodosInFileAsync(file);

            foreach (var todo in todos)
            {
                Log.WriteInformation(new FileLine(todo.File.FullName, todo.Line), todo.Todo);
                cc.AddOne(todo.File.FullName);
            }

            return file;
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



[Description("list line counts")]
internal sealed class GroupWithTimeCommand : AsyncCommand<GroupWithTimeCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var root = new DirectoryInfo(Environment.CurrentDirectory);

        var todo_list = new List<TodoInFile>();

        var source_files = TodoComments.ListFiles(root);
        await TodoComments.Progress().RunArrayAsync(source_files, async file =>
        {
            var todos = await TodoComments.FindTodosInFileAsync(file);
            todo_list.AddRange(todos);
            return file;
        });

        // group on file
        var grouped = todo_list.OrderBy(x => x.File.Name)
            .GroupBy(todo => todo.File,
                (file, todos) => new {File=file, Todos=todos.ToImmutableArray()}
                ).ToImmutableArray();

        // group by file to only run blame once (per file)
        var todo_with_blame = await TodoComments.Progress().MapArrayAsync(grouped, async entry =>
        {
            var blames = await Shared.Git.BlameAsync(entry.File).ToListAsync();
            return ($"Blaming {entry.File.FullName}", entry.Todos
                .Select(x => new {Todo = x, Blame = blames[x.Line-1] })
                .ToImmutableArray());
        });

        // extract from file grouping and order by time
        var sorted_todos = todo_with_blame.SelectMany(x => x)
            .OrderByDescending(x => blame_to_time(x.Blame));

        // group on x time ago to break up the info dump
        var time_grouped = sorted_todos.GroupBy(x => blame_to_time(x.Blame).GetTimeAgoString(),
                (x, grouped_todos) => new {Time = x, Todos = grouped_todos.ToArray()}
            ).ToArray();

        foreach(var group in time_grouped)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{group.Time}[/]:");
            foreach (var x in group.Todos)
            {
                var todo = x.Todo;
                Log.WriteInformation(new(todo.File.FullName, todo.Line), $"{blame_to_time(x.Blame)}: {todo.Todo}");
            }
        }

        return 0;

        // use author datetime when sorting
        static DateTime blame_to_time(Shared.Git.BlameLine b) => b.Author.Time;
    }
}
