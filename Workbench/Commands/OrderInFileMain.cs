using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Workbench.Commands.OrderInFileCommands;


internal sealed class OrderInFileCommand : Command<OrderInFileCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Doxygen index.xml")]
        [CommandArgument(0, "[searchPath]")]
        public string SearchPath { get; init; } = string.Empty;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        return CommonExecute.WithPrinter(printer => OrderInFile.Run(printer, settings.SearchPath, Environment.CurrentDirectory));
    }
}

internal sealed class ClassifyClass : Command<ClassifyClass.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Doxygen index.xml")]
        [CommandArgument(0, "[searchPath]")]
        public string SearchPath { get; init; } = string.Empty;

        [Description("The class/struct name to classify")]
        [CommandArgument(0, "[className]")]
        public string ClassName { get; init; } = string.Empty;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        return CommonExecute.WithPrinter(printer => OrderInFile.ClassifyClass(printer, settings.SearchPath, settings.ClassName, Environment.CurrentDirectory));
    }
}

public static class Main
{
    public static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, git =>
        {
            git.SetDescription("Check order in a file");

            git.AddCommand<OrderInFileCommand>("check").WithDescription("Run check against everything");
            git.AddCommand<ClassifyClass>("classify").WithDescription("Classify all members in class (for debugging)");
        });
    }
}
