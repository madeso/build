using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;
using Workbench.Shared;

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
        var cwd = Dir.CurrentDirectory;
        var paths = new Config.RealPaths();
        var vfs = new VfsDisk();

        return CliUtil.PrintErrorsAtExit(print =>
        {
            Status.HandleStatus(vfs, cwd, print, settings, paths);
            return 0;
        });
    }
}
