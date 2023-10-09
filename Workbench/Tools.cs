using Spectre.Console;
using System.Collections.Immutable;
using Workbench.CompileCommands;
using Workbench.Utils;

namespace Workbench.Tools;


// todo(Gustav):
// include:
//     find out who is including XXX.h and how it's included
//     generate a union graphviz file that is a union of all includes with the TU as the root


internal static class F
{
    private static IEnumerable<string> GetGetIncludeDirectories(string path, IReadOnlyDictionary<string, CompileCommand> cc)
    {
        var c = cc[path];
        foreach (var relativeInclude in c.GetRelativeIncludes())
        {
            yield return new FileInfo(Path.Join(c.directory, relativeInclude)).FullName;
        }
    }


    private static IEnumerable<string> FindIncludeFiles(string path)
    {
        var lines = File.ReadAllLines(path);
        foreach (var line in lines)
        {
            var l = line.Trim();
            // beware... shitty c++ parser
            if (l.StartsWith("#include"))
            {
                yield return l.Split(" ")[1].Trim().Trim('\"').Trim('<').Trim('>');
            }
        }
    }


    private static string? ResolveIncludeViaIncludeDirectoriesOrNone(string include, IEnumerable<string> includeDirectories)
    {
        return includeDirectories
            .Select(directory => Path.Join(directory, include))
            .FirstOrDefault(File.Exists)
            ;
    }


    private static bool IsLimited(string realFile, IEnumerable<string> limit)
    {
        var hasLimits = false;

        foreach (var l in limit)
        {
            hasLimits = true;
            if (realFile.StartsWith(l))
            {
                return false;
            }
        }

        return hasLimits;
    }


    private static string CalculateIdentifier(string file, string? name)
    {
        return name ?? file.Replace("/", "_").Replace(".", "_").Replace("-", "_").Trim('_');
    }


    private static string CalculateDisplay(string file, string? name, string root)
    {
        return name ?? Path.GetRelativePath(root, file);
    }


    private static string GetGroup(string relativeFile)
    {
        var b = relativeFile.Split("/");
        if (b.Length == 1)
        {
            return "";
        }
        var r = (new string[] { "libs", "external" }).Contains(b[0]) ? b[1] : b[0];
        if (r == "..")
        {
            return "";
        }
        return r;
    }

    // todo(Gustav): merge with global Graphviz
    class Graphvizer
    {
        Dictionary<string, string> nodes = new(); // id -> name
        ColCounter<string> links = new(); // link with counts

        private record GroupedItems(string Group, KeyValuePair<string, string>[] Items);

        public IEnumerable<string> GetLines(bool group, bool isCluster)
        {
            yield return "digraph G";
            yield return "{";
            var grouped = group
                ? this.nodes.GroupBy(x => GetGroup(x.Value),
                        (groupName, items) => new GroupedItems(groupName, items.ToArray()))
                    .ToArray()
                : new GroupedItems[] { new("", this.nodes.ToArray()) }
                ;
            foreach (var (groupTitle, items) in grouped)
            {
                var hasGroup = groupTitle != "";
                var indent = hasGroup ? "    " : "";

                if (hasGroup)
                {
                    var clusterPrefix = isCluster ? "cluster_" : "";
                    yield return $"subgraph {clusterPrefix}{groupTitle}";
                    yield return "{";
                }

                foreach (var (identifier, name) in items)
                {
                    yield return $"{indent}{identifier} [label=\"{name}\"];";
                }

                if (hasGroup)
                {
                    yield return "}";
                }
            }
            yield return "";
            foreach (var (code, count) in this.links.Items)
            {
                yield return $"{code} [label=\"{count}\"];";
            }

            yield return "}";
            yield return "";
        }

        public void Link(string source, string? name, string resolved, string root)
        {
            var fromNode = CalculateIdentifier(source, name);
            var toNode = CalculateIdentifier(resolved, null);
            this.links.AddOne($"{fromNode} -> {toNode}");

            // probably will calc more than once but who cares?
            this.nodes[fromNode] = CalculateDisplay(source, name, root);
            this.nodes[toNode] = CalculateDisplay(resolved, null, root);
        }
    }


    private static void gv_work(string realFile, string? name, ImmutableArray<string> includeDirectories, Graphvizer gv,
        ImmutableArray<string> limit, string root)
    {
        if (IsLimited(realFile, limit))
        {
            return;
        }

        foreach (var include in FindIncludeFiles(realFile))
        {
            var resolved = ResolveIncludeViaIncludeDirectoriesOrNone(include, includeDirectories);
            if (resolved != null)
            {
                gv.Link(realFile, name, resolved, root);
                // todo(Gustav): fix name to be based on root
                gv_work(resolved, null, includeDirectories, gv, limit, root);
            }
            else
            {
                gv.Link(realFile, name, include, root);
            }
        }
    }


    private static void Work(Printer print, string realFile, ImmutableArray<string> includeDirectories,
        ColCounter<string> counter, bool printFiles, ImmutableArray<string> limit)
    {
        if (IsLimited(realFile, limit))
        {
            return;
        }

        foreach (var include in FindIncludeFiles(realFile))
        {
            var resolved = ResolveIncludeViaIncludeDirectoriesOrNone(include, includeDirectories);
            if (resolved != null)
            {
                if (printFiles)
                {
                    print.Info(resolved);
                }
                counter.AddOne(resolved);
                Work(print, resolved, includeDirectories, counter, printFiles, limit);
            }
            else
            {
                if (printFiles)
                {
                    print.Info($"Unable to find {include}");
                }
                counter.AddOne(include);
            }
        }
    }


    private static void PrintMostCommon(Printer print, ColCounter<string> counter, int mostCommonCount)
    {
        foreach (var (file, count) in counter.MostCommon().Take(mostCommonCount))
        {
            print.Info($"{file}: {count}");
        }
    }


    private static IEnumerable<string> GetAllTranslationUnits(IEnumerable<string> files)
    {
        return files
            .SelectMany(path => FileUtil.ListFilesRecursively(path, FileUtil.SourceFiles))
            .Select(file => new FileInfo(file).FullName)
            ;
    }


    private static IEnumerable<string> FileReadLines(string path, bool discardEmpty)
    {
        var lines = File.ReadLines(path);

        if (!discardEmpty)
        {
            return lines;
        }

        return lines
            .Where(line => string.IsNullOrWhiteSpace(line) == false)
            ;

    }


    private static int GetLineCount(string path, bool discardEmpty)
    {
        return FileReadLines(path, discardEmpty)
            .Count();
    }


    private static IEnumerable<int> GetAllIndent(string path, bool discardEmpty)
    {
        return FileReadLines(path, discardEmpty)
                .Select(line => line.Length - line.TrimStart().Length)
            ;
    }


    private static int GetMaxIndent(string path, bool discardEmpty)
    {
        return GetAllIndent(path, discardEmpty)
            .DefaultIfEmpty(-1)
            .Max();
    }


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
        var totalFiles = 0;

        foreach (var file in FileUtil.ListFilesRecursively(files, FileUtil.HeaderFiles))
        {
            totalFiles += 1;
            if (ContainsPragmaOnce(file))
            {
                continue;
            }

            print.Info(file);
            count += 1;
        }

        print.Info($"Found {count} in {totalFiles} files.");
        return 0;
    }

    public static int HandleListIndents(Printer print, string[] argsFiles, int each, bool argsShow,
        bool argsHist, bool discardEmpty)
    {
        var stats = new Dictionary<int, List<string>>();
        var foundFiles = 0;

        foreach (var file in FileUtil.ListFilesRecursively(argsFiles, FileUtil.HeaderAndSourceFiles))
        {
            foundFiles += 1;

            var counts = !argsHist
                    ? new[] { GetMaxIndent(file, discardEmpty) }
                    : GetAllIndent(file, discardEmpty)
                        .Order()
                        .Distinct()
                        .ToArray()
                ;

            foreach (var count in counts)
            {
                var index = each <= 1
                        ? count
                        : count - (count % each)
                    ;
                if (stats.TryGetValue(index, out var values))
                {
                    values.Add(file);
                }
                else
                {
                    stats.Add(index, new List<string> { file });
                }
            }
        }

        var allSorted = stats.OrderBy(x => x.Key)
            .Select(x => (
                each <= 1 ? $"{x.Key}" : $"{x.Key}-{x.Key + each - 1}",
                x.Key,
                x.Value
            ))
            .ToImmutableArray();
        print.Info($"Found {foundFiles} files.");

        if (argsHist)
        {
            var chart = new BarChart()
                .Width(60)
                .Label("[green bold underline]Number of files / indentation[/]")
                .CenterLabel();
            foreach (var (label, start, files) in allSorted)
            {
                chart.AddItem(label, files.Count, Color.Green);
            }
            AnsiConsole.Write(chart);
        }
        else
        {
            foreach (var (label, start, files) in allSorted)
            {
                if (argsShow)
                {
                    print.Info($"{label}: {files}");
                }
                else
                {
                    print.Info($"{label}: {files.Count}");
                }
            }
        }

        return 0;
    }

    private static ImmutableArray<string> CompleteLimitArg(IEnumerable<string> argsLimit)
        => argsLimit.Select(x => new DirectoryInfo(x).FullName).ToImmutableArray();

    public static int HandleListCommand(Printer print, string? compileCommandsArg,
        string[] args_files,
        bool args_print_files,
        bool args_print_stats,
        bool printMax,
        bool printList,
        int args_count,
        string[] args_limit)
    {
        if (compileCommandsArg == null)
        {
            return -1;
        }
        var compile_commands = CompileCommands.F.LoadCompileCommandsOrNull(print, compileCommandsArg);
        if (compile_commands == null)
        {
            return -1;
        }

        var totalCounter = new ColCounter<string>();
        var maxCounter = new ColCounter<string>();

        var limit = CompleteLimitArg(args_limit);

        var numberOfTranslationUnits = 0;

        foreach (var translationUnit in GetAllTranslationUnits(args_files))
        {
            numberOfTranslationUnits += 1;
            var fileCounter = new ColCounter<string>();
            if (compile_commands.ContainsKey(translationUnit))
            {
                var includeDirectories = GetGetIncludeDirectories(translationUnit, compile_commands).ToImmutableArray();
                Work(print, translationUnit, includeDirectories, fileCounter, args_print_files, limit);
            }

            if (args_print_stats)
            {
                PrintMostCommon(print, fileCounter, 10);
            }
            totalCounter.Update(fileCounter);
            maxCounter.Max(fileCounter);
        }

        if (printMax)
        {
            print.Info("");
            print.Info("");
            print.Info("10 top number of includes for a translation unit");
            PrintMostCommon(print, maxCounter, 10);
        }

        if (printList)
        {
            print.Info("");
            print.Info("");
            print.Info("Number of includes per translation unit");
            foreach (var (file, count) in totalCounter.Items.OrderBy(x => x.Key))
            {
                if (count >= args_count)
                {
                    print.Info($"{file} included in {count}/{numberOfTranslationUnits}");
                }
            }
        }

        print.Info("");
        print.Info("");

        return 0;
    }

    public static int HandleGraphvizCommand(Printer print, string? compileCommandsArg,
        string[] args_files,
        string[] args_limit,
        bool args_group,
        bool clusterOutput)
    {
        if (compileCommandsArg is null)
        {
            return -1;
        }

        var compileCommands = CompileCommands.F.LoadCompileCommandsOrNull(print, compileCommandsArg);
        if (compileCommands == null) { return -1; }

        var limit = CompleteLimitArg(args_limit);

        var gv = new Graphvizer();

        foreach (var translationUnit in GetAllTranslationUnits(args_files))
        {
            if (compileCommands.ContainsKey(translationUnit))
            {
                var includeDirectories = GetGetIncludeDirectories(translationUnit, compileCommands).ToImmutableArray();
                gv_work(translationUnit, "TU", includeDirectories, gv, limit, Environment.CurrentDirectory);
            }
        }

        foreach (var line in gv.GetLines(args_group || clusterOutput, clusterOutput))
        {
            print.Info(line);
        }

        return 0;
    }

    public static int HandleListNoProjectFolderCommand(Printer print, string[] argsFiles, string? compile_commands_arg, string cmake)
    {
        if (compile_commands_arg == null) { return -1; }

        var bases = argsFiles.Select(FileUtil.RealPath).ToImmutableArray();

        var buildRoot = new FileInfo(compile_commands_arg).Directory?.FullName;
        if (buildRoot == null) { return -1; }

        var projects = new HashSet<string>();
        var projectsWithFolders = new HashSet<string>();
        var files = new Dictionary<string, string>();
        var projectFolders = new ColCounter<string>();

        foreach (var cmd in CMake.Trace.TraceDirectory(cmake, buildRoot))
        {
            if (bases.Select(b => FileIsInFolder(cmd.File, b)).Any())
            {
                if ((new[] { "add_library", "add_executable" }).Contains(cmd.Cmd.ToLowerInvariant()))
                {
                    var projectName = cmd.Args[0];
                    if (cmd.Args[1] != "INTERFACE")
                    { // skip interface libraries
                        projects.Add(projectName);
                        files[projectName] = cmd.File;
                    }
                }
                if (cmd.Cmd.ToLowerInvariant() == "set_target_properties")
                {
                    // set_target_properties(core PROPERTIES FOLDER "Libraries")
                    if (cmd.Args[1] == "PROPERTIES" && cmd.Args[2] == "FOLDER")
                    {
                        projectsWithFolders.Add(cmd.Args[0]);
                        projectFolders.AddOne(cmd.Args[3]);
                    }
                }
            }
        }

        // var sort_on_cmake = lambda x: x[1];
        var missing = projects
            .Where(x => projectsWithFolders.Contains(x) == false)
            .Select(m => new { Missing = m, File = files[m] })
            .OrderBy(x => x.File)
            .ToImmutableArray();
        var totalMissing = missing.Length;

        int missingFiles = 0;

        var grouped = missing.GroupBy(x => x.File, (g, list) => new { cmake_file = g, sorted_files = list.Select(x => x.Missing).OrderBy(x => x).ToImmutableArray() });
        foreach (var g in grouped)
        {
            missingFiles += 1;
            print.Info(Path.GetRelativePath(Environment.CurrentDirectory, g.cmake_file));
            foreach (var f in g.sorted_files)
            {
                print.Info($"    {f}");
            }
            if (g.sorted_files.Length > 1)
            {
                print.Info($"    = {g.sorted_files.Length} projects");
            }
            print.Info("");
        }
        print.Info($"Found missing: {totalMissing} projects in {missingFiles} files");
        PrintMostCommon(print, projectFolders, 10);

        return 0;
    }


    public static int HandleMissingInCmakeCommand(Printer print, string[] args_files, string? build_root, string cmake)
    {
        var bases = args_files.Select(FileUtil.RealPath).ToImmutableArray();
        if (build_root == null) { return -1; }

        var paths = new HashSet<string>();

        foreach (var cmd in CMake.Trace.TraceDirectory(cmake, build_root))
        {
            if (bases.Select(b => FileIsInFolder(cmd.File, b)).Any())
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
        foreach (var file in FileUtil.ListFilesRecursively(args_files, FileUtil.HeaderAndSourceFiles))
        {
            var resolved = new FileInfo(file).FullName;
            if (paths.Contains(resolved) == false)
            {
                print.Info(resolved);
                count += 1;
            }
        }

        print.Info($"Found {count} files not referenced in cmake");
        return 0;
    }

    public static int HandleLineCountCommand(Printer print, string[] args_files,
        int each,
        bool args_show,
        bool args_discard_empty)
    {
        var stats = new Dictionary<int, List<string>>();
        var fileCount = 0;

        foreach (var file in FileUtil.ListFilesRecursively(args_files, FileUtil.HeaderAndSourceFiles))
        {
            fileCount += 1;

            var count = GetLineCount(file, args_discard_empty);

            var index = each <= 1 ? count : count - (count % each);
            if (stats.TryGetValue(index, out var dataValues))
            {
                dataValues.Add(file);
            }
            else
            {
                stats.Add(index, new List<string> { file });
            }
        }

        print.Info($"Found {fileCount} files.");
        foreach (var (count, files) in stats.OrderBy(x => x.Key))
        {
            var c = files.Count;
            var countStr = each <= 1 ? $"{count}" : $"{count}-{count + each - 1}";
            if (args_show && c < 3)
            {
                print.Info($"{countStr}: {files}");
            }
            else
            {
                print.Info($"{countStr}: {c}");
            }
        }

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

        print.Info($"Found {errors} errors in {files} files.");

        return errors > 0 ? -1 : 0;
    }
}
