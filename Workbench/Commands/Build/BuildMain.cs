using Spectre.Console.Cli;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Workbench.Shared;
using Workbench.Shared.CMake;
using Workbench.Shared.Extensions;

namespace Workbench.Commands.Build;


internal class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, cmake =>
        {
            cmake.SetDescription("Build commands for windows");
            cmake.AddCommand<SetupCommand2>("setup").WithDescription("Setup the build folders using cmake");
        });
    }
}


internal sealed class SetupCommand2 : AsyncCommand<SetupCommand2.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("No OPeration")]
        [CommandOption("--nop")]
        [DefaultValue(false)]
        public bool Nop { get; set; }

        [Description("Don't abort if the parent folder contains a cmake file")]
        [CommandOption("--no-parent-check")]
        [DefaultValue(false)]
        public bool NoParentCheck { get; set; }

        [Description("Don't setup a debug build")]
        [CommandOption("--debug")]
        [DefaultValue(false)]
        public bool Debug { get; set; }

        [Description("Don't setup a release build")]
        [CommandOption("--release")]
        [DefaultValue(false)]
        public bool Release { get; set; }

        [Description("Don't setup a afl compatible compiler")]
        [CommandOption("--afl")]
        [DefaultValue(false)]
        public bool Afl { get; set; }

        [Description("If build exists, don't abort")]
        [CommandOption("--force")]
        [DefaultValue(false)]
        public bool Force { get; set; }

        [Description("The compiler and platforms to use")]
        [CommandArgument(0, "<compiler|platform>")]
        public required CompilersAndPlatforms[] CompilersAndPlatforms { get; set; }
    }


    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var cwd = Dir.CurrentDirectory;
        var vfs = new VfsDisk();
        var exec = new SystemExecutor();

        return await CliUtil.PrintErrorsAtExitAsync(async print =>
            await new SetupRunner(exec, vfs, cwd, print, settings.Nop, settings.Force).Run(settings)
        );

    }
}

internal class SetupRunner(Executor exec, Vfs vfs, Dir cwd, Log log, bool nop, bool force)
{
    private bool HasCMake(Dir folder)
        => folder.GetFile("CMakeLists.txt").Exists(vfs);

    internal async Task<int> Run(SetupCommand2.Arg settings)
    {
        if (!HasCMake(cwd))
        {
            Console.WriteLine("Folder does not look like a cmake project, aborting...");
            return 1;
        }

        if (!settings.NoParentCheck)
        {
            var parent = cwd.Parent;
            if (parent != null && HasCMake(parent))
            {
                Console.WriteLine("parent folder has cmake, aborting...");
                return 1;
            }
        }

        var build_folder = cwd.GetSubDirs("build");
        var compilers = settings.CompilersAndPlatforms.Select(x => x.to_compiler()).IgnoreNull().ToImmutableArray();
        if (compilers.IsEmpty)
        {
            Console.WriteLine("ERR: Need at least 1 compiler");
            return 1;
        }

        var has_many_compilers = compilers.Select(BuildFunctions.IsVisualStudio).Distinct().Count() > 1;
        if (has_many_compilers)
        {
            Console.WriteLine("ERR: Both windows and linux compilers listed!");
            return 1;
        }

        var auto_platform = Core.Is64Bit() ? Platform.X64 : Platform.Win32;
        var platforms = settings.CompilersAndPlatforms
            // only requested platforms
            .Select(x => x.to_platform())
            .IgnoreNull()
            // replace auto
            .Select(x => x != Platform.Auto ? x : auto_platform)
            // no duplicates
            .Distinct()
            // if not platforms, include at least the current
            .DefaultIfEmpty(auto_platform)
            .ToImmutableArray();

        if (compilers.Where(BuildFunctions.IsVisualStudio).Any())
        {
            // windows
            if (settings.Debug || settings.Release)
            {
                Console.WriteLine("Windows don't need debug/release setup");
                return 1;
            }

            if (settings.Afl)
            {
                Console.WriteLine("AFL is not supported on windows");
                return 1;
            }
        }
        else
        {
            // linux
            if (settings.Debug == false && settings.Release == false)
            {
                Console.WriteLine("Multi config is not supported");
                return 1;
            }

            if (platforms.Any(x => x != auto_platform))
            {
                Console.WriteLine("Linux is only supporting the auto platform");
                return 1;
            }
        }

        foreach (var compiler in compilers)
        {
            foreach (var platform in platforms)
            {
                var name = compiler.name_of_compiler();
                if (settings.Debug)
                {
                    await SetupCMake(cwd, build_folder.GetSubDirs($"debug-{name}"), "Debug", compiler, platform);
                }

                if (settings.Afl)
                {
                    await SetupCMake(cwd, build_folder.GetSubDirs($"afl-{name}"), "Debug", compiler, platform, afl: true);
                }

                if (settings.Release)
                {
                    await SetupCMake(cwd, build_folder.GetSubDirs($"release-{name}"), "Release", compiler, platform);
                }

                if (BuildFunctions.IsVisualStudio(compiler))
                {
                    await SetupCMake(cwd, build_folder.GetSubDirs(name), null, compiler, platform);
                }
            }
        }
        return 0;
    }

    private async Task SetupCMake(Dir source_folder, Dir build_folder, string? cmake_build_type, Compiler compiler, Platform platform, bool afl = false)
    {
        if (vfs.DirectoryExists(build_folder) && force == false)
        {
            log.Warning($"Build directory exists {build_folder.Path}, ignoring...");
            return;
        }
        build_folder.CreateDir(vfs);
        var compilers = compiler.find_names(afl);
        var gen = BuildFunctions.CreateCmakeGenerator(compiler, platform);

        var proj = new CMakeProject(build_folder, source_folder, gen);
        if (compilers != null)
        {
            proj.AddArgument("CMAKE_C_COMPILER", compilers.C);
            proj.AddArgument("CMAKE_CXX_COMPILER", compilers.Cpp);
        }

        if (cmake_build_type != null)
        {
            proj.AddArgument("CMAKE_BUILD_TYPE", cmake_build_type);
        }

        await proj.ConfigureAsync(exec, vfs, cwd, log, nop);

        /*
        var psi = new ProcessStartInfo
        {
            FileName = "cmake",
            ArgumentList =
            {
                "-G", "Ninja",
                "-D", $"CMAKE_C_COMPILER={c_compiler}",
                "-D", $"CMAKE_CXX_COMPILER={cpp_compiler}",
                "-D", $"CMAKE_BUILD_TYPE={cmake_build_type}",
                source_folder
            },
            WorkingDirectory = build_folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var proc = Process.Start(psi);
        if (proc == null)
        {
            Console.WriteLine($"Failed to create process from cmake in {build_folder}");
            return false;
        }

        proc.WaitForExit();
        if (proc.ExitCode != 0)
        {
            Console.WriteLine($"cmake failed for {build_folder}:\n" + proc.StandardError.ReadToEnd());
            return false;
        }

        return true;
        */
    }
}
