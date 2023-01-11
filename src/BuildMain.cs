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
                name = new DirectoryInfo(Environment.CurrentDirectory).Name
            };
            data.includes.Add(new() { "list of regexes", "that are used by check-includes" });
            data.includes.Add(new() { "they are grouped into arrays, there needs to be a space between each group" });
            data.dependencies.AddRange(Enum.GetValues<DependencyName>());

            var content = JsonUtil.Write(data);
            if (settings.Overwrite == false && File.Exists(path))
            {
                print.error($"{path} already exist and overwrite was not requested");
                return -1;
            }

            File.WriteAllText(path, content);
            print.info($"Wrote {path}");
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
                F.save_build(printer, build, data);
                F.run_install(build, data, printer);
                return 0;
            })
        );
    }
}


internal sealed class CmakeCommand : Command<CmakeCommand.Arg>
{
    public sealed class Arg : EnviromentArgument
    {
        // [Description("Simplify dotfile output")]
        [CommandOption("--print")]
        [DefaultValue(false)]
        public bool Print { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print => F.handle_generic_build(
            print, settings,
            (printer, build, data) =>
            {
                F.save_build(printer, build, data);
                F.run_cmake(build, data, printer, settings.Print);
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
                F.save_build(printer, build, data);
                F.run_install(build, data, printer);
                F.run_cmake(build, data, printer, false);
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
                F.save_build(printer, build, data);
                F.generate_cmake_project(build, data).build(printer);
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
        var project = new CMake.CMake(data.build_dir, data.root_dir, build.get_cmake_generator());

        foreach (var dep in data.dependencies)
        {
            dep.add_cmake_arguments(project);
        }

        return project;
    }


    // install dependencies
    internal static void run_install(BuildEnviroment env, BuildData data, Printer print)
    {
        foreach (var dep in data.dependencies)
        {
            dep.install(env, print, data);
        }
    }


    // configure the euphoria cmake project
    internal static void run_cmake(BuildEnviroment build, BuildData data, Printer printer, bool only_print)
    {
        generate_cmake_project(build, data).config_with_print(printer, only_print);
    }

    // save the build environment to the settings file
    internal static void save_build(Printer print, BuildEnviroment build, BuildData data)
    {
        Core.verify_dir_exist(print, data.build_base_dir);
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

        printer.info($"Project: {data.name}");
        printer.info($"Enviroment: {env}");
        printer.info("");
        printer.info($"Data: {data.get_path_to_settings()}");
        printer.info($"Root: {data.root_dir}");
        printer.info($"Build: {data.build_dir}");
        printer.info($"Dependencies: {data.dependency_dir}");
        var indent = "    ";
        foreach (var dep in data.dependencies)
        {
            printer.info($"{indent}{dep.get_name()}");
            var lines = dep.status();
            foreach (var line in lines)
            {
                printer.info($"{indent}{indent}{line}");
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
