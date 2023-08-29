using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Workbench.Commands.DependenciesCommands;


internal sealed class ListCommand : Command<ListCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Doxygen xml folder")]
        [CommandArgument(0, "[searchPath]")]
        public string DoxygenXml { get; init; } = string.Empty;

        [Description("Namespace filter")]
        [CommandArgument(0, "[name]")]
        public string NamespaceFilter { get; init; } = string.Empty;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        return CommonExecute.WithPrinter(printer =>
            {
                Dependencies.Run(printer, arg.DoxygenXml, arg.NamespaceFilter);
                return 0;
            }
        );
    }
}

public static class Main
{
    public static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, root =>
        {
            root.SetDescription("Dependency commands");

            root.AddCommand<ListCommand>("list").WithDescription("List dependencies");
        });
    }
}
