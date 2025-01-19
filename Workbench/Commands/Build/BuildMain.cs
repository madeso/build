using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Spectre.Console.Cli;
using Workbench.Shared;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
        var cwd = Dir.CurrentDirectory;
        var vfs = new VfsDisk();

        return CliUtil.PrintErrorsAtExit(print => BuildFacade.HandleInit(vfs, cwd, print, settings.Overwrite));
    }
}

internal sealed class StatusCommand : AsyncCommand<StatusCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var cwd = Dir.CurrentDirectory;
        var vfs = new VfsDisk();

        return await BuildFacade.WithLoadedBuildDataAsync(vfs, cwd, async (log, data) =>
        {
            await Task.Delay(0); // hack since all build data are async
            return BuildFacade.HandleBuildStatus(vfs, log, data);
        });
    }
}

internal sealed class InstallCommand : AsyncCommand<InstallCommand.Arg>
{
    public sealed class Arg : EnvironmentArgument
    {
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var cwd = Dir.CurrentDirectory;
        var vfs = new VfsDisk();

        return await BuildFacade.HandleGenericBuildAsync(vfs, cwd, settings,
            (_, printer, env, data) => BuildFacade.HandleInstallAsync(vfs, cwd, printer, env, data));
    }
}


internal sealed class CmakeCommand : AsyncCommand<CmakeCommand.Arg>
{
    public sealed class Arg : EnvironmentArgument
    {
        [Description("Don't run command, only print")]
        [CommandOption("--nop")]
        [DefaultValue(false)]
        public bool Nop { get; set; }
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var cwd = Dir.CurrentDirectory;
        var vfs = new VfsDisk();

        return await BuildFacade.HandleGenericBuildAsync(vfs, cwd, settings,
            (_, printer, env, data) => BuildFacade.HandleCmakeAsync(vfs, cwd, settings.Nop, printer, env, data));
    }
}


internal sealed class DevCommand : AsyncCommand<DevCommand.Arg>
{
    public sealed class Arg : EnvironmentArgument
    {
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var cwd = Dir.CurrentDirectory;
        var vfs = new VfsDisk();

        return await BuildFacade.HandleGenericBuildAsync(vfs, cwd, settings,
            (_, printer, env, data) => BuildFacade.HandleDevAsync(vfs, cwd, printer, env, data));
    }
}


internal sealed class BuildCommand : AsyncCommand<BuildCommand.Arg>
{
    public sealed class Arg : EnvironmentArgument
    {
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var cwd = Dir.CurrentDirectory;
        var vfs = new VfsDisk();

        return await BuildFacade.HandleGenericBuildAsync(vfs, cwd, settings,
            (_, printer, env, data) => BuildFacade.HandleBuildAsync(vfs, cwd, printer, env, data));
    }
}
