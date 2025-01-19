using System.Collections.Immutable;
using Spectre.Console.Cli;
using Spectre.Console;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Workbench.Shared;
using Workbench.Shared.CMake;
using Workbench.Shared.Extensions;
using static Workbench.Shared.Reflect;

namespace Workbench.Commands.CheckForMissingInCmake;


/*
Previous commands for refactoring info:
   wb tools check-missing-in-cmake
   wb tools check-missing-in-cmake libs
   wb tools check-missing-in-cmake libs apps
 */
[Description("find files that exists on disk but are missing in cmake")]
internal sealed class CheckForMissingInCmakeCommand : AsyncCommand<CheckForMissingInCmakeCommand.Arg>
{
    public sealed class Arg : CompileCommandsArguments
    {
        [Description("Folders to read")]
        [CommandArgument(0, "<input files>")]
        public string[] Folders { get; set; } = Array.Empty<string>();
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg args)
    {
        return await CliUtil.PrintErrorsAtExitAsync(async print =>
        {
            var cwd = Dir.CurrentDirectory;
            var vfs = new VfsDisk();

            var cmake = FindCMake.RequireInstallationOrNull(vfs, print);
            if (cmake == null)
            {
                print.Error("Failed to find cmake");
                return -1;
            }

            var build_root = FindCMake.RequireBuildOrNone(vfs, cwd, args, print);
            if (build_root == null) { return -1; }

            var bases = Cli.ToDirectories(vfs, cwd, print, args.Folders)
                .ToImmutableArray();

            var paths = new HashSet<Fil>();
            foreach (var cmd in await CMakeTrace.TraceDirectoryAsync(cwd, cmake, build_root))
            {
                if (!bases.Select(b => FileUtil.FileIsInFolder(cmd.File!, b)).Any())
                {
                    continue;
                }

                var exeFiles = cmd.Cmd.ToLower() == "add_executable" ? cmd.ListFilesInCmakeExecutable() : Enumerable.Empty<FileInCmake>();
                var files = cmd.Cmd.ToLower() == "add_library" ? cmd.ListFilesInLibraryOrExecutable() : exeFiles;
                foreach (var f in files)
                {
                    var full = f.File.Path;
                    var ends = f.Name.Replace("/", "\\").Replace("\\\\", "\\");
                    while(ends.StartsWith("."))
                    {
                        ends = ends.TrimStart('.').TrimStart('\\');
                    }
                    if (full.EndsWith(ends) == false)
                    {
                        AnsiConsole.WriteLine("Hrm...");
                    }
                    paths.Add(f.File);
                }
            }

            var count = 0;
            foreach (var file in FileUtil.SourcesFromArgs(vfs, cwd, args.Folders, FileUtil.IsHeaderOrSource))
            {
                var resolved = file;
                if (paths.Contains(resolved) == false)
                {
                    AnsiConsole.WriteLine($"{resolved}");
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
