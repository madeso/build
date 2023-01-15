using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Workbench.Commands;

internal static class MinorCommands
{
    internal static void ConfigureCat(IConfigurator config, string v)
    {
        config.AddCommand<CatCommand>(v).WithDescription("Print the contents of a single file");
    }

    internal static void ConfigureLs(IConfigurator config, string v)
    {
        config.AddCommand<LsCommand>(v).WithDescription("Print the tree of a directory");
    }
}

internal sealed class CatCommand : Command<CatCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("File to print")]
        [CommandArgument(0, "<input file>")]
        public string Path { get; set; } = "";
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print =>
        {
            print.PrintContentsOfFile(settings.Path);
            return 0;
        });
    }
}

internal sealed class LsCommand : Command<LsCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Directoy to list")]
        [CommandArgument(0, "<input file>")]
        public string Path { get; set; } = "";
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print =>
        {
            print.PrintDirectoryStructure(settings.Path);
            return 0;
        });
    }
}
