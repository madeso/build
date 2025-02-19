﻿using Spectre.Console.Cli;
using Spectre.Console;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Workbench.Shared;
using Workbench.Shared.CMake;

namespace Workbench.Commands.CheckForNoProjectFolders;



/*
Previous commands for refactoring info:
   wb tools check-no-project-folders libs apps
   wb tools check-no-project-folders libs apps external
   wb tools check-no-project-folders libs apps external tools data
 */
[Description("find projects that have not set the solution folder")]
internal sealed class CheckForNoProjectFoldersCommand : AsyncCommand<CheckForNoProjectFoldersCommand.Arg>
{
    public sealed class Arg : CompileCommandsArguments
    {
        [Description("Directories to read")]
        [CommandArgument(0, "<directories>")]
        public string[] Directories { get; set; } = Array.Empty<string>();
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var cwd = Dir.CurrentDirectory;
        var vfs = new VfsDisk();
        var exec = new SystemExecutor();

        return await CliUtil.PrintErrorsAtExitAsync(async log =>
        {
            var cmake = FindCMake.RequireInstallationOrNull(vfs, log);
            if (cmake == null)
            {
                return -1;
            }

            var build_root = FindCMake.ListAllBuilds(vfs, cwd, settings)
                .RequireFirstValueOrNull(log, "build root");
            if (build_root == null)
            {
                return -1;
            }

            return await CheckForNoProjectFolders(exec, cwd, Cli.ToDirectories(vfs, cwd, log, settings.Directories), build_root, cmake);
        });
    }

    public static async Task<int> CheckForNoProjectFolders(Executor exec, Dir cwd, IEnumerable<Dir> bases_iter, Dir build_root, Fil cmake)
    {
        var projects = new HashSet<string>();
        var projects_with_folders = new HashSet<string>();
        var files = new Dictionary<string, Fil>();
        var project_folders = new ColCounter<string>();

        var bases = bases_iter.ToImmutableArray();

        foreach (var cmd in await CMakeTrace.TraceDirectoryAsync(exec, cwd, cmake, build_root))
        {
            if(cmd.File == null) continue;
            if (bases.Select(b => FileUtil.FileIsInFolder(cmd.File, b)).Any())
            {
                if ((new[] { "add_library", "add_executable" }).Contains(cmd.Cmd.ToLowerInvariant()))
                {
                    var project_name = cmd.Args[0];
                    if (cmd.Args.Length<=1 || cmd.Args[1] != "INTERFACE")
                    { // skip interface libraries
                        projects.Add(project_name);
                        files[project_name] = cmd.File;
                    }
                }
                if (cmd.Cmd.ToLowerInvariant() == "set_target_properties")
                {
                    // set_target_properties(core PROPERTIES FOLDER "Libraries")
                    if (cmd.Args[1] == "PROPERTIES" && cmd.Args[2] == "FOLDER")
                    {
                        projects_with_folders.Add(cmd.Args[0]);
                        project_folders.AddOne(cmd.Args[3]);
                    }
                }
            }
        }

        // var sort_on_cmake = lambda x: x[1];
        var missing = projects
            .Where(x => projects_with_folders.Contains(x) == false)
            .Select(m => new { Missing = m, File = files[m] })
            .OrderBy(x => x.File)
            .ToImmutableArray();
        var total_missing = missing.Length;

        int missing_files = 0;

        var grouped = missing.GroupBy(x => x.File, (g, list) => new { cmake_file = g, sorted_files = list.Select(x => x.Missing).OrderBy(x => x).ToImmutableArray() });
        foreach (var g in grouped)
        {
            missing_files += 1;
            AnsiConsole.WriteLine(g.cmake_file.GetDisplay(cwd));
            foreach (var f in g.sorted_files)
            {
                AnsiConsole.WriteLine($"    {f}");
            }
            if (g.sorted_files.Length > 1)
            {
                AnsiConsole.WriteLine($"    = {g.sorted_files.Length} projects");
            }
            AnsiConsole.WriteLine("");
        }


        AnsiConsole.WriteLine($"Found missing: {total_missing} projects in {missing_files} files");
        project_folders.PrintMostCommon(10);

        // todo(Gustav): return error when things are missing
        return 0;
    }
}



public static class Main
{
    public static void Configure(IConfigurator config, string name)
    {
        config.AddCommand<CheckForNoProjectFoldersCommand>(name);
    }
}
