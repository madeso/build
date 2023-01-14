using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Workbench.Build;

namespace Workbench.Commands.BuildCommands;


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
        return CommonExecute.WithPrinter(print =>
        {
            return F.HandleInit(print, settings.Overwrite);
        });
    }
}

internal sealed class StatusCommand : Command<StatusCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print =>
        {
            F.HandleBuildStatus(print);
            return 0;
        });
    }
}

internal sealed class InstallCommand : Command<InstallCommand.Arg>
{
    public sealed class Arg : EnviromentArgument
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print => F.HandleGenericBuild(
            print, settings,
            (printer, build, data) =>
            {
                return F.HandleInstall(printer, build, data);
            })
        );
    }
}


internal sealed class CmakeCommand : Command<CmakeCommand.Arg>
{
    public sealed class Arg : EnviromentArgument
    {
        [Description("Don't run command, only print")]
        [CommandOption("--nop")]
        [DefaultValue(false)]
        public bool Nop { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print => F.HandleGenericBuild(
            print, settings,
            (printer, build, data) =>
            {
                return F.HandleCmake(settings.Nop, printer, build, data);
            })
        );
    }
}


internal sealed class DevCommand : Command<DevCommand.Arg>
{
    public sealed class Arg : EnviromentArgument
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print => F.HandleGenericBuild(
            print, settings,
            (printer, build, data) =>
            {
                return F.HandleDev(printer, build, data);
            })
        );
    }
}


internal sealed class BuildCommand : Command<BuildCommand.Arg>
{
    public sealed class Arg : EnviromentArgument
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print => F.HandleGenericBuild(
            print, settings,
            (printer, build, data) =>
            {
                return F.HandleBuild(printer, build, data);
            })
        );
    }
}
