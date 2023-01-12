using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Workbench.MinorCommands;

namespace Workbench.Build;


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
            cmake.AddCommand<DebugCommand>("dev").WithDescription("Dev is install+cmake");
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
            var path = BuildData.GetBuildDataPath(Environment.CurrentDirectory);
            var data = new ProjectFile
            {
                Name = new DirectoryInfo(Environment.CurrentDirectory).Name
            };
            data.IncludeDirectories.Add(new() { "list of regexes", "that are used by check-includes" });
            data.IncludeDirectories.Add(new() { "they are grouped into arrays, there needs to be a space between each group" });
            data.Dependencies.AddRange(Enum.GetValues<DependencyName>());

            var content = JsonUtil.Write(data);
            if (settings.Overwrite == false && File.Exists(path))
            {
                print.error($"{path} already exist and overwrite was not requested");
                return -1;
            }

            File.WriteAllText(path, content);
            print.Info($"Wrote {path}");
            return 0;
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
            F.handle_build_status(print);
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
        return CommonExecute.WithPrinter(print => F.handle_generic_build(
            print, settings,
            (printer, build, data) =>
            {
                F.SaveBuildData(printer, build, data);
                F.RunInstall(build, data, printer);
                return 0;
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
        return CommonExecute.WithPrinter(print => F.handle_generic_build(
            print, settings,
            (printer, build, data) =>
            {
                F.SaveBuildData(printer, build, data);
                F.RunCmake(build, data, printer, settings.Nop);
                return 0;
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
        return CommonExecute.WithPrinter(print => F.handle_generic_build(
            print, settings,
            (printer, build, data) =>
            {
                F.SaveBuildData(printer, build, data);
                F.RunInstall(build, data, printer);
                F.RunCmake(build, data, printer, false);
                return 0;
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
        return CommonExecute.WithPrinter(print => F.handle_generic_build(
            print, settings,
            (printer, build, data) =>
            {
                F.SaveBuildData(printer, build, data);
                F.generate_cmake_project(build, data).Build(printer);
                return 0;
            })
        );
    }
}


internal static class F
{
    // generate the ride project
    internal static CMake.CMake generate_cmake_project(BuildEnviroment build, BuildData data)
    {
        var project = new CMake.CMake(data.ProjectDirectory, data.RootDirectory, build.get_cmake_generator());

        foreach (var dep in data.Dependencies)
        {
            dep.AddCmakeArguments(project);
        }

        return project;
    }


    // install dependencies
    internal static void RunInstall(BuildEnviroment env, BuildData data, Printer print)
    {
        foreach (var dep in data.Dependencies)
        {
            dep.Install(env, print, data);
        }
    }


    // configure the euphoria cmake project
    internal static void RunCmake(BuildEnviroment build, BuildData data, Printer printer, bool nop)
    {
        generate_cmake_project(build, data).Configure(printer, nop);
    }

    // save the build environment to the settings file
    internal static void SaveBuildData(Printer print, BuildEnviroment build, BuildData data)
    {
        Core.VerifyDirectoryExists(print, data.BuildDirectory);
        BuildUitls.save_to_file(build, data.get_path_to_settings());
    }

    internal static void handle_build_status(Printer printer)
    {
        var loaded_data = BuildData.load(printer);
        if (loaded_data == null)
        {
            printer.error("Unable to load the data");
            return;
        }
        var data = loaded_data.Value;
        var env = BuildUitls.load_from_file(data.get_path_to_settings(), printer);

        printer.Info($"Project: {data.name}");
        printer.Info($"Enviroment: {env}");
        printer.Info("");
        printer.Info($"Data: {data.get_path_to_settings()}");
        printer.Info($"Root: {data.RootDirectory}");
        printer.Info($"Build: {data.ProjectDirectory}");
        printer.Info($"Dependencies: {data.DependencyDirectory}");
        var indent = "    ";
        foreach (var dep in data.Dependencies)
        {
            printer.Info($"{indent}{dep.GetName()}");
            var lines = dep.GetStatus();
            foreach (var line in lines)
            {
                printer.Info($"{indent}{indent}{line}");
            }
        }
    }

    internal static int handle_generic_build(Printer printer, EnviromentArgument args, Func<Printer, BuildEnviroment, BuildData, int> callback)
    {
        var loaded_data = BuildData.load(printer);
        if (loaded_data == null)
        {
            printer.error("Unable to load the data");
            return -1;
        }
        var data = loaded_data.Value;
        var env = BuildUitls.load_from_file(data.get_path_to_settings(), printer);
        env.update_from_args(printer, args);
        if (env.validate(printer) == false)
        {
            return -1;
        }

        return callback(printer, env, data);
    }
}
