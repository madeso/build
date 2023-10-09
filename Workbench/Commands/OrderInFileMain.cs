using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Workbench.Commands.OrderInFileCommands;


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
        return CommonExecute.WithPrinter(printer => OrderInFile.Run(printer, arg.DoxygenXml, Environment.CurrentDirectory));
    }
}

internal sealed class ClassifyClass : Command<ClassifyClass.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Doxygen xml folder")]
        [CommandArgument(0, "[doxygen]")]
        public string DoxygenXml { get; init; } = string.Empty;

        [Description("The class/struct name to classify")]
        [CommandArgument(0, "[className]")]
        public string ClassName { get; init; } = string.Empty;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        return CommonExecute.WithPrinter(printer => OrderInFile.ClassifyClass(arg.DoxygenXml, arg.ClassName));
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
