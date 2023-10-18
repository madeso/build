using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;
using Workbench.Utils;

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
        return Printer.PrintErrorsAtExit(printer => CheckNamesRunner.Run(printer, arg.DoxygenXml, Environment.CurrentDirectory));
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
        return Printer.PrintErrorsAtExit(print => CheckNamesRunner.HandleInit(print, settings.Overwrite));
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
