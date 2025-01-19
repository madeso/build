using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;
using Workbench.Shared;

namespace Workbench.Commands.CheckOrderInFile;


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
        var cwd = Dir.CurrentDirectory;
        var vfs = new VfsDisk();

        return CliUtil.PrintErrorsAtExit(printer =>
        {
            var dox = Cli.RequireDirectory(vfs, cwd, printer, arg.DoxygenXml, "doxygen xml folder");
            if (dox == null)
            {
                return -1;
            }
            return OrderInFile.Run(vfs, printer, dox, cwd);
        });
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
        var cwd = Dir.CurrentDirectory;
        var vfs = new VfsDisk();

        return CliUtil.PrintErrorsAtExit(log =>
        {
            var dox = Cli.RequireDirectory(vfs, cwd, log, arg.DoxygenXml, "doxygen xml folder");
            if (dox == null)
            {
                return -1;
            }
            return OrderInFile.ClassifyClass(dox, arg.ClassName);
        });
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
