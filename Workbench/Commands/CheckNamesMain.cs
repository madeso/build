using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Workbench.Config;

namespace Workbench.Commands.CheckNamesCommands;


internal sealed class OrderInFileCommand : Command<OrderInFileCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Doxygen xml folder")]
        [CommandArgument(0, "[searchPath]")]
        public string DoxygenXml { get; init; } = string.Empty;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        return CommonExecute.WithPrinter(printer =>
        {
            var loaded = CheckNamesFile.LoadFromDirectoryOrNull(printer);
            if(loaded == null)
            {
                return -1;
            }
            return CheckNames.Run(printer, arg.DoxygenXml, Environment.CurrentDirectory, loaded);
        });
    }
}

public static class Main
{
    public static void Configure(IConfigurator config, string name)
    {
        config.AddCommand<OrderInFileCommand>(name).WithDescription("Check the names of classes");
    }
}
