using System.Collections.Immutable;
using Spectre.Console;
using Workbench.Utils;

namespace Workbench.Commands.Tools;





internal static class Tools
{
    private static bool ContainsPragmaOnce(string path)
    {
        return File.ReadLines(path)
            .Any(line => line.Contains("#pragma once"))
            ;
    }


    private static bool FileIsInFolder(string file, string folder)
    {
        return new FileInfo(file).FullName.StartsWith(new DirectoryInfo(folder).FullName);
    }



    ///////////////////////////////////////////////////////////////////////////
    //// handlers

    public static int MissingIncludeGuardsCommand(Printer print, string[] files)
    {
        var count = 0;
        var total_files = 0;

        foreach (var file in FileUtil.ListFilesRecursively(files, FileUtil.HeaderFiles))
        {
            total_files += 1;
            if (ContainsPragmaOnce(file))
            {
                continue;
            }

            AnsiConsole.WriteLine(file);
            count += 1;
        }

        AnsiConsole.WriteLine($"Found {count} in {total_files} files.");
        return 0;
    }



    public static int HandleListNoProjectFolderCommand(
        Printer print, string[] args_files, string build_root, string cmake)
    {
        var bases = args_files.Select(FileUtil.RealPath).ToImmutableArray();

        var projects = new HashSet<string>();
        var projects_with_folders = new HashSet<string>();
        var files = new Dictionary<string, string>();
        var project_folders = new ColCounter<string>();

        foreach (var cmd in CMake.Trace.TraceDirectory(cmake, build_root))
        {
            if (cmd.File == null) { continue; }

            if (bases.Select(b => FileIsInFolder(cmd.File, b)).Any())
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
            AnsiConsole.WriteLine(Path.GetRelativePath(Environment.CurrentDirectory, g.cmake_file));
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


    public static int HandleMissingInCmakeCommand(Printer print, string[] relative_files, string? build_root, string cmake)
    {
        var bases = relative_files.Select(FileUtil.RealPath).ToImmutableArray();
        if (build_root == null) { return -1; }

        var paths = new HashSet<string>();

        foreach (var cmd in CMake.Trace.TraceDirectory(cmake, build_root))
        {
            if (bases.Select(b => FileIsInFolder(cmd.File!, b)).Any())
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


    public static int HandleCheckFilesCommand(Printer print, string[] args_files)
    {
        var files = 0;
        var errors = 0;

        foreach (var file in FileUtil.ListFilesRecursively(args_files, FileUtil.HeaderAndSourceFiles))
        {
            files += 1;
            if (file.Contains('-'))
            {
                errors += 1;
                print.Error($"file name mismatch: {file}");
            }
        }

        AnsiConsole.WriteLine($"Found {errors} errors in {files} files.");

        return errors > 0 ? -1 : 0;
    }
}
