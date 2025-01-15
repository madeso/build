using Spectre.Console;
using Workbench.Config;
using Workbench.Shared;
using Workbench.Shared.CMake;

namespace Workbench.Commands.Build;



internal static class BuildFacade
{
    public static int HandleInit(Dir cwd, Log print, bool overwrite)
    {
        var data = new BuildFile
        {
            Name = cwd.Name
        };
        data.Dependencies.AddRange(Enum.GetValues<DependencyName>());

        return ConfigFile.WriteInit(print, overwrite, BuildFile.GetBuildDataPath(cwd), data);
    }

    public static async Task<int> HandleInstallAsync(Dir cwd, Log log, BuildEnvironment build, BuildData data)
    {
        SaveBuildData(log, build, data);
        await RunInstallAsync(cwd, build, data, log);
        return 0;
    }

    internal static async Task<int> HandleBuildAsync(Dir cwd, Log log, BuildEnvironment build, BuildData data)
    {
        SaveBuildData(log, build, data);
        await GenerateCmakeProjectAsync(build, data).BuildAsync(cwd, log, Shared.CMake.Config.Release);
        return 0;
    }

    internal static async Task<int> HandleDevAsync(Dir cwd, Log log, BuildEnvironment build, BuildData data)
    {
        SaveBuildData(log, build, data);
        await RunInstallAsync(cwd, build, data, log);
        await RunCmakeAsync(cwd, build, data, log, false);
        return 0;
    }

    public static async Task<int> HandleCmakeAsync(Dir cwd, bool nop, Log log, BuildEnvironment build, BuildData data)
    {
        SaveBuildData(log, build, data);
        await RunCmakeAsync(cwd, build, data, log, nop);
        return 0;
    }

    // generate the ride project
    internal static CMakeProject GenerateCmakeProjectAsync(BuildEnvironment build, BuildData data)
    {
        var project = new CMakeProject(data.ProjectDirectory, data.RootDirectory, build.CreateCmakeGenerator());

        foreach (var dep in data.Dependencies)
        {
            dep.AddCmakeArguments(project);
        }

        return project;
    }


    // install dependencies
    internal static async Task RunInstallAsync(Dir cwd, BuildEnvironment env, BuildData data, Log print)
    {
        foreach (var dep in data.Dependencies)
        {
            await dep.InstallAsync(cwd, env, print, data);
        }
    }


    // configure the euphoria cmake project
    internal static async Task RunCmakeAsync(Dir cwd, BuildEnvironment build, BuildData data, Log log, bool nop)
    {
        await GenerateCmakeProjectAsync(build, data).ConfigureAsync(cwd, log, nop);
    }

    // save the build environment to the settings file
    internal static void SaveBuildData(Log print, BuildEnvironment build, BuildData data)
    {
        Core.VerifyDirectoryExists(print, data.BuildDirectory);
        BuildFunctions.SaveToFile(build, data.GetPathToSettingsFile());
    }

    internal static int HandleBuildStatus(Log log, BuildData data)
    {
        var env = BuildFunctions.LoadFromFileOrCreateEmpty(data.GetPathToSettingsFile(), log);

        Printer.Header(data.Name);
        AnsiConsole.WriteLine($"Project: {data.Name}");
        AnsiConsole.WriteLine($"Dependencies: {data.Dependencies.Count}");
        AnsiConsole.WriteLine("Environment:");
        AnsiConsole.WriteLine($"  Compiler: {EnumTools.GetString(env.Compiler) ?? "missing"}");
        AnsiConsole.WriteLine($"  Platform: {EnumTools.GetString(env.Platform) ?? "missing"}");
        AnsiConsole.WriteLine("");
        AnsiConsole.WriteLine("Folders:");
        AnsiConsole.WriteLine($"  Data: {data.GetPathToSettingsFile()}");
        AnsiConsole.WriteLine($"  Root: {data.RootDirectory}");
        AnsiConsole.WriteLine($"  Build: {data.ProjectDirectory}");
        AnsiConsole.WriteLine($"  Dependencies: {data.DependencyDirectory}");
        const string INDENT = "    ";
        if (data.Dependencies.Count > 0)
        {
            AnsiConsole.WriteLine("");
        }
        foreach (var dep in data.Dependencies)
        {
            AnsiConsole.WriteLine($"{INDENT}{dep.GetName()}");
            var lines = dep.GetStatus();
            foreach (var line in lines)
            {
                AnsiConsole.WriteLine($"{INDENT}{INDENT}{line}");
            }
        }

        return 0;
    }

    public static async Task<int> WithLoadedBuildDataAsync(Dir cwd, Func<Log, BuildData, Task<int>> callback)
    {
        return await CliUtil.PrintErrorsAtExitAsync(async print =>
        {
            var data = BuildData.LoadOrNull(cwd, print);
            if (data == null)
            {
                print.Error("Unable to load the data");
                return -1;
            }
            else
            {
                return await callback(print, data.Value);
            }
        });
    }

    internal static async Task<int> HandleGenericBuildAsync(Dir cwd, EnvironmentArgument args, Func<Dir, Log, BuildEnvironment, BuildData, Task<int>> callback)
    {
        return await WithLoadedBuildDataAsync(cwd, async (printer, data) =>
        {
            var env = BuildFunctions.LoadFromFileOrCreateEmpty(data.GetPathToSettingsFile(), printer);
            env.UpdateFromArguments(printer, args);
            if (env.Validate(printer) == false)
            {
                return -1;
            }

            return await callback(cwd, printer, env, data);
        });
    }
}
