using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;
using Workbench.Shared;

namespace Workbench.Commands.CheckNames;


internal sealed class CheckCommand : Command<CheckCommand.Arg>
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
            return CheckNamesRunner.Run(vfs, cwd, printer, dox, cwd);
        });
    }
}

internal sealed class InitCommand : Command<InitCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("If output exists, force overwrite")]
        [CommandOption("--overwrite")]
        [DefaultValue(false)]
        public bool Overwrite { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var cwd = Dir.CurrentDirectory;
        var vfs = new VfsDisk();

        return CliUtil.PrintErrorsAtExit(print => CheckNamesRunner.HandleInit(vfs, cwd, print, settings.Overwrite));
    }
}

public static class Main
{
    public static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, cmake =>
        {
            cmake.SetDescription("Check the names of classes, functions and other things");

            cmake.AddCommand<InitCommand>("init").WithDescription("Create a check names command");
            
            cmake.AddCommand<CheckCommand>("check").WithDescription("Run the check");
        });
        
    }
}
