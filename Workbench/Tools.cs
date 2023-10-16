using System.Collections.Immutable;
using Spectre.Console;
using Workbench.Utils;

namespace Workbench;


// todo(Gustav):
// include:
//     find out who is including XXX.h and how it's included
//     generate a union graphviz file that is a union of all includes with the TU as the root


internal static class Tools
{
    private static IEnumerable<string> GetGetIncludeDirectories(string path, IReadOnlyDictionary<string, CompileCommand> cc)
    {
        var c = cc[path];
        foreach (var relative_include in c.GetRelativeIncludes())
        {
            yield return new FileInfo(Path.Join(c.Directory, relative_include)).FullName;
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


    private static string? ResolveIncludeViaIncludeDirectoriesOrNone(string include, IEnumerable<string> include_directories)
    {
        return include_directories
            .Select(directory => Path.Join(directory, include))
            .FirstOrDefault(File.Exists)
            ;
    }


    private static bool IsLimited(string real_file, IEnumerable<string> limit)
    {
        var has_limits = false;

        foreach (var l in limit)
        {
            has_limits = true;
            if (real_file.StartsWith(l))
            {
                return false;
            }
        }

        return has_limits;
    }


    private static string CalculateIdentifier(string file, string? name)
    {
        return name ?? file.Replace("/", "_").Replace(".", "_").Replace("-", "_").Trim('_');
    }


    private static string CalculateDisplay(string file, string? name, string root)
    {
        return name ?? Path.GetRelativePath(root, file);
    }


    private static string GetGroup(string relative_file)
    {
        var b = relative_file.Split("/");
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
        private readonly Dictionary<string, string> nodes = new(); // id -> name
        private readonly ColCounter<string> links = new(); // link with counts

        private record GroupedItems(string Group, KeyValuePair<string, string>[] Items);

        public IEnumerable<string> GetLines(bool group, bool is_cluster)
        {
            yield return "digraph G";
            yield return "{";
            var grouped = group
                ? this.nodes.GroupBy(x => GetGroup(x.Value),
                        (group_name, items) => new GroupedItems(group_name, items.ToArray()))
                    .ToArray()
                : new GroupedItems[] { new("", this.nodes.ToArray()) }
                ;
            foreach (var (group_title, items) in grouped)
            {
                var has_group = group_title != "";
                var indent = has_group ? "    " : "";

                if (has_group)
                {
                    var cluster_prefix = is_cluster ? "cluster_" : "";
                    yield return $"subgraph {cluster_prefix}{group_title}";
                    yield return "{";
                }

                foreach (var (identifier, name) in items)
                {
                    yield return $"{indent}{identifier} [label=\"{name}\"];";
                }

                if (has_group)
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
            var from_node = CalculateIdentifier(source, name);
            var to_node = CalculateIdentifier(resolved, null);
            this.links.AddOne($"{from_node} -> {to_node}");

            // probably will calc more than once but who cares?
            this.nodes[from_node] = CalculateDisplay(source, name, root);
            this.nodes[to_node] = CalculateDisplay(resolved, null, root);
        }
    }


    private static void gv_work(string real_file, string? name, ImmutableArray<string> include_directories, Graphvizer gv,
        ImmutableArray<string> limit, string root)
    {
        if (IsLimited(real_file, limit))
        {
            return;
        }

        foreach (var include in FindIncludeFiles(real_file))
        {
            var resolved = ResolveIncludeViaIncludeDirectoriesOrNone(include, include_directories);
            if (resolved != null)
            {
                gv.Link(real_file, name, resolved, root);
                // todo(Gustav): fix name to be based on root
                gv_work(resolved, null, include_directories, gv, limit, root);
            }
            else
            {
                gv.Link(real_file, name, include, root);
            }
        }
    }


    private static void Work(Printer print, string 
            real_file, ImmutableArray<string> include_directories,
        ColCounter<string> counter, bool print_files, ImmutableArray<string> limit)
    {
        if (IsLimited(real_file, limit))
        {
            return;
        }

        foreach (var include in FindIncludeFiles(real_file))
        {
            var resolved = ResolveIncludeViaIncludeDirectoriesOrNone(include, include_directories);
            if (resolved != null)
            {
                if (print_files)
                {
                    Printer.Info(resolved);
                }
                counter.AddOne(resolved);
                Work(print, resolved, include_directories, counter, print_files, limit);
            }
            else
            {
                if (print_files)
                {
                    Printer.Info($"Unable to find {include}");
                }
                counter.AddOne(include);
            }
        }
    }


    private static void PrintMostCommon(Printer print, ColCounter<string> counter, int most_common_count)
    {
        foreach (var (file, count) in counter.MostCommon().Take(most_common_count))
        {
            Printer.Info($"{file}: {count}");
        }
    }


    private static IEnumerable<string> GetAllTranslationUnits(IEnumerable<string> files)
    {
        return files
            .SelectMany(path => FileUtil.ListFilesRecursively(path, FileUtil.SourceFiles))
            .Select(file => new FileInfo(file).FullName)
            ;
    }


    private static IEnumerable<string> FileReadLines(string path, bool discard_empty)
    {
        var lines = File.ReadLines(path);

        if (!discard_empty)
        {
            return lines;
        }

        return lines
            .Where(line => string.IsNullOrWhiteSpace(line) == false)
            ;

    }


    private static int GetLineCount(string path, bool discard_empty)
    {
        return FileReadLines(path, discard_empty)
            .Count();
    }


    private static IEnumerable<int> GetAllIndent(string path, bool discard_empty)
    {
        return FileReadLines(path, discard_empty)
                .Select(line => line.Length - line.TrimStart().Length)
            ;
    }


    private static int GetMaxIndent(string path, bool discard_empty)
    {
        return GetAllIndent(path, discard_empty)
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
        var total_files = 0;

        foreach (var file in FileUtil.ListFilesRecursively(files, FileUtil.HeaderFiles))
        {
            total_files += 1;
            if (ContainsPragmaOnce(file))
            {
                continue;
            }

            Printer.Info(file);
            count += 1;
        }

        Printer.Info($"Found {count} in {total_files} files.");
        return 0;
    }

    public static int HandleListIndents(Printer print, string[] args_files, int each, bool args_show,
        bool args_hist, bool discard_empty)
    {
        var stats = new Dictionary<int, List<string>>();
        var found_files = 0;

        foreach (var file in FileUtil.ListFilesRecursively(args_files, FileUtil.HeaderAndSourceFiles))
        {
            found_files += 1;

            var counts = !args_hist
                    ? new[] { GetMaxIndent(file, discard_empty) }
                    : GetAllIndent(file, discard_empty)
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

        var all_sorted = stats.OrderBy(x => x.Key)
            .Select(x => (
                each <= 1 ? $"{x.Key}" : $"{x.Key}-{x.Key + each - 1}",
                x.Key,
                x.Value
            ))
            .ToImmutableArray();
        Printer.Info($"Found {found_files} files.");

        if (args_hist)
        {
            var chart = new BarChart()
                .Width(60)
                .Label("[green bold underline]Number of files / indentation[/]")
                .CenterLabel();
            foreach (var (label, start, files) in all_sorted)
            {
                chart.AddItem(label, files.Count, Color.Green);
            }
            AnsiConsole.Write(chart);
        }
        else
        {
            foreach (var (label, start, files) in all_sorted)
            {
                if (args_show)
                {
                    Printer.Info($"{label}: {files}");
                }
                else
                {
                    Printer.Info($"{label}: {files.Count}");
                }
            }
        }

        return 0;
    }

    private static ImmutableArray<string> CompleteLimitArg(IEnumerable<string> args_limit)
        => args_limit.Select(x => new DirectoryInfo(x).FullName).ToImmutableArray();

    public static int HandleListCommand(Printer print, string? compile_commands_arg,
        string[] args_files,
        bool args_print_files,
        bool args_print_stats,
        bool print_max,
        bool print_list,
        int args_count,
        string[] args_limit)
    {
        if (compile_commands_arg == null)
        {
            return -1;
        }
        var compile_commands = CompileCommand.LoadCompileCommandsOrNull(print, compile_commands_arg);
        if (compile_commands == null)
        {
            return -1;
        }

        var total_counter = new ColCounter<string>();
        var max_counter = new ColCounter<string>();

        var limit = CompleteLimitArg(args_limit);

        var number_of_translation_units = 0;

        foreach (var translation_unit in GetAllTranslationUnits(args_files))
        {
            number_of_translation_units += 1;
            var file_counter = new ColCounter<string>();
            if (compile_commands.ContainsKey(translation_unit))
            {
                var include_directories = GetGetIncludeDirectories(translation_unit, compile_commands).ToImmutableArray();
                Work(print, translation_unit, include_directories, file_counter, args_print_files, limit);
            }

            if (args_print_stats)
            {
                PrintMostCommon(print, file_counter, 10);
            }
            total_counter.Update(file_counter);
            max_counter.Max(file_counter);
        }

        if (print_max)
        {
            Printer.Info("");
            Printer.Info("");
            Printer.Info("10 top number of includes for a translation unit");
            PrintMostCommon(print, max_counter, 10);
        }

        if (print_list)
        {
            Printer.Info("");
            Printer.Info("");
            Printer.Info("Number of includes per translation unit");
            foreach (var (file, count) in total_counter.Items.OrderBy(x => x.Key))
            {
                if (count >= args_count)
                {
                    Printer.Info($"{file} included in {count}/{number_of_translation_units}");
                }
            }
        }

        Printer.Info("");
        Printer.Info("");

        return 0;
    }

    public static int HandleGraphvizCommand(Printer print, string? compile_commands_arg,
        string[] args_files,
        string[] args_limit,
        bool args_group,
        bool cluster_output)
    {
        if (compile_commands_arg is null)
        {
            return -1;
        }

        var compile_commands = CompileCommand.LoadCompileCommandsOrNull(print, compile_commands_arg);
        if (compile_commands == null) { return -1; }

        var limit = CompleteLimitArg(args_limit);

        var gv = new Graphvizer();

        foreach (var translation_unit in GetAllTranslationUnits(args_files))
        {
            if (compile_commands.ContainsKey(translation_unit))
            {
                var include_directories = GetGetIncludeDirectories(translation_unit, compile_commands).ToImmutableArray();
                gv_work(translation_unit, "TU", include_directories, gv, limit, Environment.CurrentDirectory);
            }
        }

        foreach (var line in gv.GetLines(args_group || cluster_output, cluster_output))
        {
            Printer.Info(line);
        }

        return 0;
    }

    public static int HandleListNoProjectFolderCommand(
        Printer print, string[] args_files, string? compile_commands_arg, string cmake)
    {
        if (compile_commands_arg == null) { return -1; }

        var bases = args_files.Select(FileUtil.RealPath).ToImmutableArray();

        var build_root = new FileInfo(compile_commands_arg).Directory?.FullName;
        if (build_root == null) { return -1; }

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
                    if (cmd.Args[1] != "INTERFACE")
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
            Printer.Info(Path.GetRelativePath(Environment.CurrentDirectory, g.cmake_file));
            foreach (var f in g.sorted_files)
            {
                Printer.Info($"    {f}");
            }
            if (g.sorted_files.Length > 1)
            {
                Printer.Info($"    = {g.sorted_files.Length} projects");
            }
            Printer.Info("");
        }
        Printer.Info($"Found missing: {total_missing} projects in {missing_files} files");
        PrintMostCommon(print, project_folders, 10);

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
                Printer.Info(resolved);
                count += 1;
            }
        }

        Printer.Info($"Found {count} files not referenced in cmake");
        return 0;
    }

    public static int HandleLineCountCommand(Printer print, string[] args_files,
        int each,
        bool args_show,
        bool args_discard_empty)
    {
        var stats = new Dictionary<int, List<string>>();
        var file_count = 0;

        foreach (var file in FileUtil.ListFilesRecursively(args_files, FileUtil.HeaderAndSourceFiles))
        {
            file_count += 1;

            var count = GetLineCount(file, args_discard_empty);

            var index = each <= 1 ? count : count - (count % each);
            if (stats.TryGetValue(index, out var data_values))
            {
                data_values.Add(file);
            }
            else
            {
                stats.Add(index, new List<string> { file });
            }
        }

        Printer.Info($"Found {file_count} files.");
        foreach (var (count, files) in stats.OrderBy(x => x.Key))
        {
            var c = files.Count;
            var count_str = each <= 1 ? $"{count}" : $"{count}-{count + each - 1}";
            if (args_show && c < 3)
            {
                Printer.Info($"{count_str}: {files}");
            }
            else
            {
                Printer.Info($"{count_str}: {c}");
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

        Printer.Info($"Found {errors} errors in {files} files.");

        return errors > 0 ? -1 : 0;
    }
}
