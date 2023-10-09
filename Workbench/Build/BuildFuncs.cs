using Workbench.Config;
using Workbench.Utils;

namespace Workbench.Build;



internal static class F
{
    public static int HandleInit(Printer print, bool overwrite)
    {
        var data = new BuildFile
        {
            Name = new DirectoryInfo(Environment.CurrentDirectory).Name
        };
        data.Dependencies.AddRange(Enum.GetValues<DependencyName>());

        return ConfigFile.WriteInit(print, overwrite, BuildFile.GetBuildDataPath(), data);
    }

    public static int HandleInstall(Printer printer, BuildEnvironment build, BuildData data)
    {
        SaveBuildData(printer, build, data);
        RunInstall(build, data, printer);
        return 0;
    }

    internal static int HandleBuild(Printer printer, BuildEnvironment build, BuildData data)
    {
        SaveBuildData(printer, build, data);
        GenerateCmakeProject(build, data).Build(printer, CMake.Config.Releaase);
        return 0;
    }

    internal static int HandleDev(Printer printer, BuildEnvironment build, BuildData data)
    {
        SaveBuildData(printer, build, data);
        RunInstall(build, data, printer);
        RunCmake(build, data, printer, false);
        return 0;
    }

    public static int HandleCmake(bool nop, Printer printer, BuildEnvironment build, BuildData data)
    {
        SaveBuildData(printer, build, data);
        RunCmake(build, data, printer, nop);
        return 0;
    }

    // generate the ride project
    internal static CMake.CMake GenerateCmakeProject(BuildEnvironment build, BuildData data)
    {
        var project = new CMake.CMake(data.ProjectDirectory, data.RootDirectory, build.CreateCmakeGenerator());

        foreach (var dep in data.Dependencies)
        {
            dep.AddCmakeArguments(project);
        }

        return project;
    }


    // install dependencies
    internal static void RunInstall(BuildEnvironment env, BuildData data, Printer print)
    {
        foreach (var dep in data.Dependencies)
        {
            dep.Install(env, print, data);
        }
    }


    // configure the euphoria cmake project
    internal static void RunCmake(BuildEnvironment build, BuildData data, Printer printer, bool nop)
    {
        GenerateCmakeProject(build, data).Configure(printer, nop);
    }

    // save the build environment to the settings file
    internal static void SaveBuildData(Printer print, BuildEnvironment build, BuildData data)
    {
        Core.VerifyDirectoryExists(print, data.BuildDirectory);
        BuildFunctions.SaveToFile(build, data.GetPathToSettingsFile());
    }

    internal static int HandleBuildStatus(Printer printer, BuildData data)
    {
        var env = BuildFunctions.LoadFromFileOrCreateEmpty(data.GetPathToSettingsFile(), printer);

        printer.Header(data.Name);
        printer.Info($"Project: {data.Name}");
        printer.Info($"Dependencies: {data.Dependencies.Count}");
        printer.Info("Environment:");
        printer.Info($"  Compiler: {EnumTools.GetString(env.Compiler) ?? "missing"}");
        printer.Info($"  Platform: {EnumTools.GetString(env.Platform) ?? "missing"}");
        printer.Info("");
        printer.Info("Folders:");
        printer.Info($"  Data: {data.GetPathToSettingsFile()}");
        printer.Info($"  Root: {data.RootDirectory}");
        printer.Info($"  Build: {data.ProjectDirectory}");
        printer.Info($"  Dependencies: {data.DependencyDirectory}");
        const string indent = "    ";
        if (data.Dependencies.Count > 0)
        {
            printer.Info("");
        }
        foreach (var dep in data.Dependencies)
        {
            printer.Info($"{indent}{dep.GetName()}");
            var lines = dep.GetStatus();
            foreach (var line in lines)
            {
                printer.Info($"{indent}{indent}{line}");
            }
        }

        return 0;
    }

    internal static int HandleGenericBuild(EnvironmentArgument args, Func<Printer, BuildEnvironment, BuildData, int> callback)
    {
        return CommonExecute.WithLoadedBuildData((printer, data) =>
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
