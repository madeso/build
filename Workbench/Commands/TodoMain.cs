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
            cmake.AddCommand<FindTodos>("find");
        });
    }
}


[Description("list line counts")]
internal sealed class FindTodos : Command<FindTodos.Arg>
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
