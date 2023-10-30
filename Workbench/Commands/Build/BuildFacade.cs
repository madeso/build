using Spectre.Console;
using Workbench.Config;
using Workbench.Shared;
using Workbench.Shared.CMake;

namespace Workbench.Commands.Build;



internal static class BuildFacade
{
    public static int HandleInit(Log print, bool overwrite)
    {
        var data = new BuildFile
        {
            Name = Dir.CurrentDirectory.Name
        };
        data.Dependencies.AddRange(Enum.GetValues<DependencyName>());

        return ConfigFile.WriteInit(print, overwrite, BuildFile.GetBuildDataPath(), data);
    }

    public static async Task<int> HandleInstallAsync(Log log, BuildEnvironment build, BuildData data)
    {
        SaveBuildData(log, build, data);
        await RunInstallAsync(build, data, log);
        return 0;
    }

    internal static async Task<int> HandleBuildAsync(Log log, BuildEnvironment build, BuildData data)
    {
        SaveBuildData(log, build, data);
        await GenerateCmakeProjectAsync(build, data).BuildAsync(log, Shared.CMake.Config.Release);
        return 0;
    }

    internal static async Task<int> HandleDevAsync(Log log, BuildEnvironment build, BuildData data)
    {
        SaveBuildData(log, build, data);
        await RunInstallAsync(build, data, log);
        await RunCmakeAsync(build, data, log, false);
        return 0;
    }

    public static async Task<int> HandleCmakeAsync(bool nop, Log log, BuildEnvironment build, BuildData data)
    {
        SaveBuildData(log, build, data);
        await RunCmakeAsync(build, data, log, nop);
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
    internal static async Task RunInstallAsync(BuildEnvironment env, BuildData data, Log print)
    {
        foreach (var dep in data.Dependencies)
        {
            await dep.InstallAsync(env, print, data);
        }
    }


    // configure the euphoria cmake project
    internal static async Task RunCmakeAsync(BuildEnvironment build, BuildData data, Log log, bool nop)
    {
        await GenerateCmakeProjectAsync(build, data).ConfigureAsync(log, nop);
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

    public static async Task<int> WithLoadedBuildDataAsync(Func<Log, BuildData, Task<int>> callback)
    {
        return await Log.PrintErrorsAtExitAsync(async print =>
        {
            var data = BuildData.LoadOrNull(print);
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

    internal static async Task<int> HandleGenericBuildAsync(EnvironmentArgument args, Func<Log, BuildEnvironment, BuildData, Task<int>> callback)
    {
        return await WithLoadedBuildDataAsync(async (printer, data) =>
        {
            var env = BuildFunctions.LoadFromFileOrCreateEmpty(data.GetPathToSettingsFile(), printer);
            env.UpdateFromArguments(printer, args);
            if (env.Validate(printer) == false)
            {
                return -1;
            }

            return await callback(printer, env, data);
        });
    }
}
