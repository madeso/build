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
        var file = settings.SearchPath ?? Directory.GetCurrentDirectory();

        return CommonExecute.WithPrinter(printer => OrderInFile.Run(printer, file, Environment.CurrentDirectory));
    }
}

public static class Main
{
    public static void Configure(IConfigurator config, string name)
    {
        config.AddCommand<OrderInFileCommand>(name).WithDescription("Check order in file");
    }
}
