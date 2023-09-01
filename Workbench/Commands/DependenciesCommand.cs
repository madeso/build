using Spectre.Console.Cli;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Workbench.Commands.DependenciesCommands;


internal sealed class ListCommand : Command<ListCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Doxygen xml folder")]
        [CommandArgument(0, "[doxygen xml]")]
        public string DoxygenXml { get; init; } = string.Empty;

        [Description("Namespace filter")]
        [CommandArgument(1, "[namespace]")]
        public string NamespaceFilter { get; init; } = string.Empty;

        [Description("Output file")]
        [CommandArgument(2, "[output]")]
        public string OutputFile { get; init; } = string.Empty;

        [Description("Ignored classes")]
        [CommandOption("-x")]
        public string[] IgnoredClasses { get; init; } = Array.Empty<string>();
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        return CommonExecute.WithPrinter(printer =>
            {
                Dependencies.Run(printer, arg.DoxygenXml, arg.NamespaceFilter, arg.OutputFile, arg.IgnoredClasses.ToImmutableHashSet());
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
