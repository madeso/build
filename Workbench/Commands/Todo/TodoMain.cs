using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;
using Workbench.Utils;

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
internal sealed class FindTodosCommand : Command<FindTodosCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var root = new DirectoryInfo(Environment.CurrentDirectory);

        var cc = new ColCounter<string>();

        foreach (var todo in TodoComments.ListAllTodos(root))
        {
            var file_and_line = Printer.ToFileString(todo.File.FullName, todo.Line);
            AnsiConsole.MarkupLineInterpolated($"[blue]{file_and_line}[/]: {todo.Todo}");
            cc.AddOne(todo.File.FullName);
        }

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
internal sealed class GroupWithTimeCommand : Command<GroupWithTimeCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var root = new DirectoryInfo(Environment.CurrentDirectory);
        var all_todos = TodoComments.ListAllTodos(root).OrderBy(x => x.File.Name);

        // group by file to only run blame once (per file)
        var todo_with_blame = all_todos.GroupBy(todo => todo.File, (file, todos) => {
                var blames = Utils.Git.Blame(file).ToArray();
                return todos.Select(x => new {Todo = x, Blame = blames[x.Line-1] });
            });

        // extract from file grouping and order by time
        var sorted_todos = todo_with_blame.SelectMany(x => x).OrderByDescending(x => blame_to_time(x.Blame));

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
                var file_and_line = Printer.ToFileString(todo.File.FullName, todo.Line);
                AnsiConsole.MarkupLineInterpolated($"[blue]{file_and_line}[/] {blame_to_time(x.Blame)}: {todo.Todo}");
            }
        }

        return 0;

        // use author datetime when sorting
        static DateTime blame_to_time(Utils.Git.BlameLine b) => b.Author.Time;
    }
}
