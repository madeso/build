using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;
using Workbench.Utils;

namespace Workbench.Commands.Build;


internal class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, cmake =>
        {
            cmake.SetDescription("Build commands for windows");
            cmake.AddCommand<InitCommand>("init").WithDescription("Create a build command");
            cmake.AddCommand<StatusCommand>("status").WithDescription("Print the status of the build");
            cmake.AddCommand<InstallCommand>("install").WithDescription("Install dependencies");
            cmake.AddCommand<CmakeCommand>("cmake").WithDescription("Configure cmake project");
            cmake.AddCommand<DevCommand>("dev").WithDescription("Dev is install+cmake");
            cmake.AddCommand<BuildCommand>("build").WithDescription("Build the project");
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
        return Printer.PrintErrorsAtExit(print => BuildFacade.HandleInit(print, settings.Overwrite));
    }
}

internal sealed class StatusCommand : Command<StatusCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return BuildFacade.WithLoadedBuildData(BuildFacade.HandleBuildStatus);
    }
}

internal sealed class InstallCommand : Command<InstallCommand.Arg>
{
    public sealed class Arg : EnvironmentArgument
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return BuildFacade.HandleGenericBuild(settings, BuildFacade.HandleInstall);
    }
}


internal sealed class CmakeCommand : Command<CmakeCommand.Arg>
{
    public sealed class Arg : EnvironmentArgument
    {
        [Description("Don't run command, only print")]
        [CommandOption("--nop")]
        [DefaultValue(false)]
        public bool Nop { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return BuildFacade.HandleGenericBuild(
            settings,
            (printer, env, data) => BuildFacade.HandleCmake(settings.Nop, printer, env, data));
    }
}


internal sealed class DevCommand : Command<DevCommand.Arg>
{
    public sealed class Arg : EnvironmentArgument
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return BuildFacade.HandleGenericBuild(settings, BuildFacade.HandleDev);
    }
}


internal sealed class BuildCommand : Command<BuildCommand.Arg>
{
    public sealed class Arg : EnvironmentArgument
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return BuildFacade.HandleGenericBuild(settings, BuildFacade.HandleBuild);
    }
}
