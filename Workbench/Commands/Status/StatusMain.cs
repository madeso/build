using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;

namespace Workbench.Commands.Status;

public static class Main
{
    internal static void ConfigureStatus(IConfigurator config, string v)
    {
        config.AddCommand<DebugCommand>(v).WithDescription("Display what workbench think of your current setup");
    }
}



internal sealed class DebugCommand : Command<DebugCommand.Arg>
{
    public sealed class Arg : CompileCommandsArguments
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print =>
        {
            Status.HandleStatus(print, settings);
            return 0;
        });
    }
}
