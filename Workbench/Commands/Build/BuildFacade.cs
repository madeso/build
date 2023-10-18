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
            Name = new DirectoryInfo(Environment.CurrentDirectory).Name
        };
        data.Dependencies.AddRange(Enum.GetValues<DependencyName>());

        return ConfigFile.WriteInit(print, overwrite, BuildFile.GetBuildDataPath(), data);
    }

    public static int HandleInstall(Log log, BuildEnvironment build, BuildData data)
    {
        SaveBuildData(log, build, data);
        RunInstall(build, data, log);
        return 0;
    }

    internal static int HandleBuild(Log log, BuildEnvironment build, BuildData data)
    {
        SaveBuildData(log, build, data);
        GenerateCmakeProject(build, data).Build(log, Shared.CMake.Config.Release);
        return 0;
    }

    internal static int HandleDev(Log log, BuildEnvironment build, BuildData data)
    {
        SaveBuildData(log, build, data);
        RunInstall(build, data, log);
        RunCmake(build, data, log, false);
        return 0;
    }

    public static int HandleCmake(bool nop, Log log, BuildEnvironment build, BuildData data)
    {
        SaveBuildData(log, build, data);
        RunCmake(build, data, log, nop);
        return 0;
    }

    // generate the ride project
    internal static CMakeProject GenerateCmakeProject(BuildEnvironment build, BuildData data)
    {
        var project = new CMakeProject(data.ProjectDirectory, data.RootDirectory, build.CreateCmakeGenerator());

        foreach (var dep in data.Dependencies)
        {
            dep.AddCmakeArguments(project);
        }

        return project;
    }


    // install dependencies
    internal static void RunInstall(BuildEnvironment env, BuildData data, Log print)
    {
        foreach (var dep in data.Dependencies)
        {
            dep.Install(env, print, data);
        }
    }


    // configure the euphoria cmake project
    internal static void RunCmake(BuildEnvironment build, BuildData data, Log log, bool nop)
    {
        GenerateCmakeProject(build, data).Configure(log, nop);
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

    public static int WithLoadedBuildData(Func<Log, BuildData, int> callback)
    {
        return Log.PrintErrorsAtExit(print =>
        {
            var data = BuildData.LoadOrNull(print);
            if (data == null)
            {
                print.Error("Unable to load the data");
                return -1;
            }
            else
            {
                return callback(print, data.Value);
            }
        });
    }

    internal static int HandleGenericBuild(EnvironmentArgument args, Func<Log, BuildEnvironment, BuildData, int> callback)
    {
        return WithLoadedBuildData((printer, data) =>
        {
            var env = BuildFunctions.LoadFromFileOrCreateEmpty(data.GetPathToSettingsFile(), printer);
            env.UpdateFromArguments(printer, args);
            if (env.Validate(printer) == false)
            {
                return -1;
            }

            return callback(printer, env, data);
        });
    }
}
