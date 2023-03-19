using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

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
        return CommonExecute.WithPrinter(print => TodoComments.PrintAll(print, new DirectoryInfo(Environment.CurrentDirectory)));
    }
}

