using System.Collections.Immutable;
using Spectre.Console.Cli;
using Spectre.Console;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Workbench.Shared;
using Workbench.Shared.CMake;

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
        [Description("Folders to read")]
        [CommandArgument(0, "<input files>")]
        public string[] Folders { get; set; } = Array.Empty<string>();
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg args)
    {
        return Log.PrintErrorsAtExit(print =>
        {
            var cmake = FindCMake.RequireInstallationOrNull(print);
            if (cmake == null)
            {
                print.Error("Failed to find cmake");
                return -1;
            }

            string? build_root = FindCMake.RequireBuildOrNone(args, print);
            if (build_root == null) { return -1; }

            var bases = args.Folders.Select(FileUtil.RealPath).ToImmutableArray();

            var paths = new HashSet<string>();
            foreach (var cmd in CMakeTrace.TraceDirectory(cmake, build_root))
            {
                if (!bases.Select(b => FileUtil.FileIsInFolder(cmd.File!, b)).Any())
                {
                    continue;
                }

                if (cmd.Cmd.ToLower() == "add_library" || cmd.Cmd.ToLower() == "add_executable")
                {
                    paths.UnionWith(cmd.ListFilesInLibraryOrExecutable());
                }
            }

            var count = 0;
            foreach (var file in FileUtil.SourcesFromArgs(args.Folders, FileUtil.HeaderAndSourceFiles))
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
        });
    }
}

public static class Main
{
    public static void Configure(IConfigurator config, string name)
    {
        config.AddCommand<CheckForMissingInCmakeCommand>(name);
    }
}
