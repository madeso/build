using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Workbench.Utils;

namespace Workbench.Commands.TodoCommands;


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
            var fileAndLine = Printer.ToFileString(todo.File.FullName, todo.Line);
            AnsiConsole.MarkupLineInterpolated($"[blue]{fileAndLine}[/]: {todo.Todo}");
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
        var todos = TodoComments.ListAllTodos(root).OrderBy(x => x.File.Name);

        // group by file to only run blame once (per file)
        var todoWithBlame = todos.GroupBy(todo => todo.File, (file, todos) => {
                var blames = Git.Blame(file).ToArray();
                return todos.Select(x => new {Todo = x, Blame = blames[x.Line-1] });
            });

        // use author datetime when sorting
        static DateTime BlameToTime(Git.BlameLine b) => b.Author.Time;

        // extract from file grouping and order by time
        var sortedTodos = todoWithBlame.SelectMany(x => x).OrderByDescending(x => BlameToTime(x.Blame));

        // group on x time ago to break up the info dump
        var timeGrouped = sortedTodos.GroupBy(x => DateUtils.TimeAgo(BlameToTime(x.Blame)),
                (x, todos) => new {Time = x, Todos = todos.ToArray()}
            ).ToArray();

        foreach(var group in timeGrouped)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{group.Time}[/]:");
            foreach (var x in group.Todos)
            {
                var todo = x.Todo;
                var fileAndLine = Printer.ToFileString(todo.File.FullName, todo.Line);
                AnsiConsole.MarkupLineInterpolated($"[blue]{fileAndLine}[/] {BlameToTime(x.Blame)}: {todo.Todo}");
            }
        }

        return 0;
    }
}
