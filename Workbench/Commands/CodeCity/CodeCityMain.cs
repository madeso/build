using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;
using Workbench.Shared;

namespace Workbench.Commands.CodeCity;


internal sealed class WriteCodeCity : Command<WriteCodeCity.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Doxygen xml folder")]
        [CommandArgument(0, "[doxygen xml]")]
        public string DoxygenXml { get; init; } = string.Empty;

        [Description("Output file")]
        [CommandArgument(2, "[output]")]
        public string OutputFile { get; init; } = string.Empty;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        return CliUtil.PrintErrorsAtExit(printer =>
            {
                var dox = Cli.RequireDirectory(printer, arg.DoxygenXml, "Doxygen xml folder");
                if (dox == null)
                {
                    return -1;
                }
                var cubes = Facade.Collect(printer, dox);
                Cli.ToSingleFile(arg.OutputFile, "code-city.html").WriteAllLines(Facade.HtmlLines("CodeCity", cubes));
                return 0;
            }
        );
    }
}

public static class Main
{
    public static void Configure(IConfigurator config, string name)
    {
        config.AddCommand<WriteCodeCity>(name)
            .WithDescription("Generate a code city from doxygen")
            ;
    }
}