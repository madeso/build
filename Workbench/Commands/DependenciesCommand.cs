using Spectre.Console.Cli;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Workbench.Commands.DependenciesCommands;


internal sealed class ListGraphvizCommand : Command<ListGraphvizCommand.Arg>
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

        [Description("Don't consider functions/methods when making connections")]
        [CommandOption("--no-functions")]
        public bool? NoIncludeFunctions { get; init; }

        [Description("Don't consider function arguments when making connections")]
        [CommandOption("--no-args")]
        public bool? NoAddArguments { get; init; }

        [Description("Don't consider any members when making connections")]
        [CommandOption("--no-members")]
        public bool? NoAddMembers { get; init; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        return CommonExecute.WithPrinter(printer =>
            {
                Dependencies.WriteToGraphviz(printer, arg.DoxygenXml, arg.NamespaceFilter, arg.OutputFile,
                    arg.IgnoredClasses.ToImmutableHashSet(),
                    !(arg.NoIncludeFunctions ?? false),
                    !(arg.NoAddArguments ?? false),
                    !(arg.NoAddMembers ?? false)
                    );
                return 0;
            }
        );
    }
}


internal sealed class PrintCommand : Command<PrintCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Doxygen xml folder")]
        [CommandArgument(0, "[doxygen xml]")]
        public string DoxygenXml { get; init; } = string.Empty;

        [Description("Namespace filter")]
        [CommandArgument(1, "[namespace]")]
        public string NamespaceFilter { get; init; } = string.Empty;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        return CommonExecute.WithPrinter(printer =>
        {
            Dependencies.PrintLists(printer, arg.DoxygenXml, arg.NamespaceFilter);
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

            root.AddCommand<ListGraphvizCommand>("list").WithDescription("Write dependencies to graphviz");
            root.AddCommand<PrintCommand>("print").WithDescription("Print all classes and functions in a namespace");
        });
    }
}
