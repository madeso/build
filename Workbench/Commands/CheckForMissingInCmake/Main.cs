using System.Collections.Immutable;
using Spectre.Console.Cli;
using Spectre.Console;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Workbench.CMake;
using Workbench.Utils;

namespace Workbench.Commands.CheckForMissingInCmake;


/*
Previous commands for refactoring info:
   wb tools check-missing-in-cmake
   wb tools check-missing-in-cmake libs
   wb tools check-missing-in-cmake libs apps
 */
[Description("find files that exists on disk but are missing in cmake")]
internal sealed class CheckForMissingInCmakeCommand : Command<CheckForMissingInCmakeCommand.Arg>
{
    public sealed class Arg : CompileCommandsArguments
    {
        [Description("File to read")]
        [CommandArgument(0, "<input files>")]
        public string[] Files { get; set; } = Array.Empty<string>();
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print =>
        {
            var cmake = CmakeTools.FindInstallationOrNull(print);
            if (cmake == null)
            {
                print.Error("Failed to find cmake");
                return -1;
            }

            return CheckForMissingInCmake(settings.Files, CMake.CmakeTools.FindBuildOrNone(settings, print), cmake);
        });
    }

    public static int CheckForMissingInCmake(string[] relative_files, string? build_root, string cmake)
    {
        var bases = relative_files.Select(FileUtil.RealPath).ToImmutableArray();
        if (build_root == null) { return -1; }

        var paths = new HashSet<string>();

        foreach (var cmd in CMake.Trace.TraceDirectory(cmake, build_root))
        {
            if (bases.Select(b => FileUtil.FileIsInFolder(cmd.File!, b)).Any())
            {
                if (cmd.Cmd.ToLower() == "add_library")
                {
                    paths.UnionWith(cmd.ListFilesInCmakeLibrary());
                }
                if (cmd.Cmd.ToLower() == "add_executable")
                {
                    paths.UnionWith(cmd.ListFilesInCmakeLibrary());
                }
            }
        }

        var count = 0;
        foreach (var file in FileUtil.ListFilesRecursively(relative_files, FileUtil.HeaderAndSourceFiles))
        {
            var resolved = new FileInfo(file).FullName;
            if (paths.Contains(resolved) == false)
            {
                AnsiConsole.WriteLine(resolved);
                count += 1;
            }
        }

        AnsiConsole.WriteLine($"Found {count} files not referenced in cmake");
        return 0;
    }
}

public static class Main
{
    public static void Configure(IConfigurator config, string name)
    {
        config.AddCommand<CheckForMissingInCmakeCommand>(name);
    }
}
