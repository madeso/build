using Spectre.Console;
using System.Text.RegularExpressions;
using Workbench.Config;
using Workbench.Shared;

namespace Workbench.Commands.Clang;


internal class Store
{
    public readonly Dictionary<string, StoredTidyUpdate> Cache = new();
}

internal record TidyOutput(string[] Output, TimeSpan Taken);
internal record StoredTidyUpdate(string[] Output, TimeSpan Taken, DateTime Modified);
internal record CategoryAndFiles(string Category, string[] Files);

internal class FileStatistics
{
    private readonly Dictionary<string, TimeSpan> data = new();

    internal void Add(string file, TimeSpan time)
    {
        data.Add(file, time);
    }

    internal void Print()
    {
        if (data.Count == 0) { return; }
        var average_value = TimeSpan.FromSeconds(data.Average(x => x.Value.TotalSeconds));
        var mi = data.MinBy(x => x.Value);
        var ma = data.MaxBy(x => x.Value);
        AnsiConsole.MarkupLineInterpolated($"average: {average_value:.2f}s");
        AnsiConsole.MarkupLineInterpolated($"max: {ma.Value:.2f}s foreach {ma.Key}");
        AnsiConsole.MarkupLineInterpolated($"min: {mi.Value:.2f}s foreach {mi.Key}");
        AnsiConsole.MarkupLineInterpolated($"{data.Count} files");
    }
}

internal class NamePrinter
{
    private readonly string name;
    private bool printed = false;

    public NamePrinter(string name)
    {
        this.name = name;
    }

    internal void Print()
    {
        if (printed) { return; }

        AnsiConsole.WriteLine(name);
        printed = true;
    }
}

internal static class ClangFacade
{
    private static string GetPathToStore(string build_folder)
    {
        return Path.Join(build_folder, FileNames.ClangTidyStore);
    }

    private static Store? LoadStore(Log print, string build_folder)
    {
        var file_name = GetPathToStore(build_folder);
        if (!File.Exists(file_name))
        {
            return new Store();
        }

        var content = File.ReadAllText(file_name);
        var loaded = JsonUtil.Parse<Store>(print, file_name, content);
        return loaded;
    }

    private static void SaveStore(string build_folder, Store data)
    {
        var file_name = GetPathToStore(build_folder);
        File.WriteAllText(file_name, JsonUtil.Write(data));
    }

    private static bool IsFileIgnoredByClangTidy(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length == 0) { return false; }

        var first_line = lines[0];
        return first_line.StartsWith("// clang-tidy: ignore");
    }

    private static string GetPathToClangTidySource(string root)
    {
        return Path.Join(root, "clang-tidy");
    }

    // return a iterator over the the "compiled" .clang-tidy lines
    private static IEnumerable<string> GenerateClangTidyAsIterator(string root)
    {
        var clang_tidy_file = File.ReadAllLines(GetPathToClangTidySource(root));
        var write = false;
        var checks = new List<string>();
        foreach (var line in clang_tidy_file)
        {
            if (write)
            {
                var l = line.TrimEnd();
                if (false == l.TrimStart().StartsWith("//"))
                {
                    yield return l;
                }
            }
            else
            {
                var stripped_line = line.Trim();
                if (stripped_line == "")
                { }
                else if (stripped_line[0] == '#')
                { }
                else if (stripped_line == "END_CHECKS")
                {
                    write = true;
                    var checks_value = string.Join(',', checks);
                    yield return $"Checks: \"{checks_value} \"";
                }
                else
                {
                    checks.Add(stripped_line);
                }
            }
        }
    }

    // print the clang-tidy "source"
    private static void PrintGeneratedClangTidy(string root)
    {
        foreach (var line in GenerateClangTidyAsIterator(root))
        {
            AnsiConsole.WriteLine(line);
        }
    }

    private static CategoryAndFiles[] MapFilesOnFirstDir(string root, IEnumerable<string> files_iterator)
     => files_iterator
            .Select(f => new { Path = f, Cat = FileUtil.GetFirstFolder(root, f) })
            // ignore external and build folders
            .Where(f => f.Cat != "external").Where(f => f.Cat.StartsWith("build") == false)
            // perform actual grouping
            .GroupBy
            (
                x => x.Cat,
                (cat, files) => new CategoryAndFiles(
                    cat,
                    files
                        // sort files
                        .Select(x => x.Path)
                        .OrderBy(Path.GetFileName)
                        .ThenByDescending(Path.GetExtension)
                        .ToArray()
                )
            ).ToArray();

    private static CategoryAndFiles[] MapAllFilesInRootOnFirstDir(string root, string[] extensions)
    {
        return MapFilesOnFirstDir(root, FileUtil.ListFilesRecursively(root, extensions));
    }

    private static bool FileMatchesAllFilters(string file, string[]? filters)
    {
        if (filters == null)
        {
            return false;
        }

        return filters.All(f => file.Contains(f) == false);
    }

    private static DateTime GetLastModification(string file)
    {
        var info = new FileInfo(file);
        return info.LastWriteTimeUtc;
    }

    private static DateTime GetLastModificationForFiles(IEnumerable<string> input_files)
     => input_files.Select(GetLastModification).Max();

    private static bool IsModificationTheLatest(IEnumerable<string> input_files, DateTime output)
    {
        return GetLastModificationForFiles(input_files) <= output;
    }

    private static TidyOutput? GetExistingOutputOrNull(Store store, string root, string source_file)
    {
        var root_file = GetPathToClangTidySource(root);

        if (store.Cache.TryGetValue(source_file, out var stored) == false)
        {
            return null;
        }

        if (IsModificationTheLatest(new[] { root_file, source_file }, stored.Modified))
        {
            return new TidyOutput(stored.Output, stored.Taken);
        }

        return null;
    }

    private static void StoreOutput(Store store, string root, string project_build_folder,
        string source_file, TidyOutput output)
    {
        var clang_tidy_source = GetPathToClangTidySource(root);

        var data = new StoredTidyUpdate(output.Output, output.Taken,
            GetLastModificationForFiles(new[] { clang_tidy_source, source_file }));
        store.Cache[source_file] = data;
        SaveStore(project_build_folder, store);
    }

    // runs clang-tidy and returns all the text output
    private static TidyOutput GetExistingOutputOrCallClangTidy(Store store, Log log, string root, bool force, string tidy_path, string project_build_folder, string source_file, bool fix)
    {
        if (false == force)
        {
            var existing_output = GetExistingOutputOrNull(store, root, source_file);
            if (existing_output != null)
            {
                return existing_output;
            }
        }

        var ret = CallClangTidy(log, tidy_path, project_build_folder, source_file, fix);
        StoreOutput(store, root, project_build_folder, source_file, ret);
        return ret;
    }

    private static TidyOutput CallClangTidy(Log log, string tidy_path, string project_build_folder, string source_file, bool fix)
    {
        var command = new ProcessBuilder(tidy_path);
        command.AddArgument("-p");
        command.AddArgument(project_build_folder);
        if (fix)
        {
            command.AddArgument("--fix");
        }
        command.AddArgument(source_file);

        var start = new DateTime();
        var output = command.RunAndGetOutput();

        if (output.ExitCode != 0)
        {
            log.Error($"Error: {output.ExitCode}");
            output.PrintOutput(log);
            // System.Exit(-1);
        }

        var end = new DateTime();
        var took = end - start;
        var ret = new TidyOutput(output.Output.Select(x => x.Line).ToArray(), took);
        return ret;
    }

    // runs the clang-tidy process, printing status to terminal
    private static (ColCounter<string> warnings, ColCounter<string> classes) RunTidy(
        Store store, Log log, string root, bool force, string tidy_path, string source_file,
        string project_build_folder, FileStatistics stats, bool short_list, NamePrinter name_printer, bool fix,
        string printable_file, string[] only)
    {
        name_printer.Print();
        var co = GetExistingOutputOrCallClangTidy(store, log, root, force, tidy_path, project_build_folder, source_file, fix);
        return CreateStatisticsAndPrintStatus(stats, short_list, printable_file, only, co);
    }

    private static (ColCounter<string> warnings, ColCounter<string> classes) CreateStatisticsAndPrintStatus(FileStatistics stats, bool short_list, string printable_file,
        string[] only, TidyOutput co)
    {
        var output = co.Output;
        var time_taken = co.Taken;
        var warnings = new ColCounter<string>();
        var classes = new ColCounter<string>();
        if (false == short_list && only.Length == 0)
        {
            AnsiConsole.WriteLine($"took {time_taken:.2f}s");
        }
        stats.Add(printable_file, time_taken);
        var print_empty = false;
        var hidden = only.Length > 0;
        foreach (var line in output)
        {
            if (line.Contains("warnings generated"))
            {
                // pass;
            }
            else if (line.Contains("Use -header-filter=.* to display errors"))
            {
                // pass
            }
            else if (line.Contains("Suppressed") && line.Contains("NOLINT)."))
            {
                // pass
            }
            else if (line.Contains("Suppressed") && line.Contains("non-user code"))
            {
                // pass
            }
            else
            {
                if (line.Contains("warning: "))
                {
                    warnings.AddOne(printable_file);
                    var tidy_class = CLANG_TIDY_WARNING_CLASS.Match(line);
                    if (tidy_class.Success)
                    {
                        var warning_classes = tidy_class.Groups[1];
                        foreach (var warning_class in warning_classes.Value.Split(','))
                        {
                            classes.AddOne(warning_class);
                            hidden = only.Length > 0;
                            if (only.Contains(warning_class))
                            {
                                hidden = false;
                            }
                        }
                    }
                }
                if (line.Trim() == "")
                {
                    if (false == hidden && print_empty)
                    {
                        AnsiConsole.WriteLine("");
                        print_empty = false;
                    }
                }
                else
                {
                    if (false == hidden)
                    {
                        print_empty = true;
                        AnsiConsole.WriteLine(line);
                    }
                }
            }
        }
        if (false == short_list && only.Length == 0)
        {
            PrintWarningCounter(classes, printable_file);
            AnsiConsole.WriteLine("");
        }
        return (warnings, classes);
    }

    // print warning counter to the console
    private static void PrintWarningCounter(ColCounter<string> project_counter, string project)
    {
        AnsiConsole.WriteLine($"{project_counter.TotalCount()} warnings in {project}.");
        foreach (var (file, count) in project_counter.MostCommon().Take(10))
        {
            AnsiConsole.WriteLine($"{file} at {count}");
        }
    }

    private static readonly Regex CLANG_TIDY_WARNING_CLASS = new(@"\[(\w+([-,]\w+)+)\]");

    // ------------------------------------------------------------------------------

    // write the .clang-tidy from the clang-tidy "source"
    private static void WriteTidyFileToDisk(string root)
    {
        var content = GenerateClangTidyAsIterator(root).ToArray();
        File.WriteAllLines(Path.Join(root, ".clang-tidy"), content);
    }

    // callback function called when running clang.py make
    internal static void HandleMakeTidyCommand(bool nop)
    {
        var root = Environment.CurrentDirectory;
        if (nop)
        {
            PrintGeneratedClangTidy(root);
        }
        else
        {
            WriteTidyFileToDisk(root);
        }
    }

    internal static int HandleTidyListFilesCommand(Log print, bool sort_files)
    {
        var root = Environment.CurrentDirectory;

        // var build_folder = CompileCommand.FindBuildRootOrNull(root);
        // if (build_folder is null)
        // {
        //     print.Error("unable to find build folder");
        //     return -1;
        // }

        var files = FileUtil.ListFilesRecursively(root, FileUtil.SourceFiles);

        if (sort_files)
        {
            var sorted = MapFilesOnFirstDir(root, files);
            foreach (var (project, source_files) in sorted)
            {
                Printer.Header(project);
                foreach (var file in source_files)
                {
                    AnsiConsole.WriteLine(file);
                }
                AnsiConsole.WriteLine("");
            }
        }
        else
        {
            foreach (var file in files)
            {
                AnsiConsole.WriteLine(file);
            }
        }

        return 0;
    }

    // callback function called when running clang.py tidy
    internal static int HandleRunClangTidyCommand(CompileCommandsArguments cc, Log log, string tidy_path, bool force, bool headers, bool short_args, bool args_nop, string[] args_filter, string[] args_only, bool args_fix)
    {
        var root = Environment.CurrentDirectory;
        var cc_file = CompileCommand.RequireOrNone(cc, log);
        if (cc_file == null)
        {
            return -1;
        }
        var project_build_folder = new FileInfo(cc_file).Directory?.FullName;
        if (project_build_folder is null)
        {
            log.Error("unable to find build folder");
            return -1;
        }

        var store = LoadStore(log, project_build_folder);
        if (store == null)
        {
            log.Error("unable to find load store");
            return -1;
        }

        WriteTidyFileToDisk(root);
        AnsiConsole.WriteLine($"using clang-tidy: {tidy_path}");

        var total_counter = new ColCounter<string>();
        var total_classes = new ColCounter<string>();
        Dictionary<string, List<string>> warnings_per_file = new();

        var data = MapAllFilesInRootOnFirstDir(root, headers ? FileUtil.HeaderAndSourceFiles : FileUtil.SourceFiles);
        var stats = new FileStatistics();

        foreach (var (project, source_files) in data)
        {
            var first_file = true;
            var project_counter = new ColCounter<string>();
            foreach (var source_file in source_files)
            {
                var printable_file = Path.GetRelativePath(root, source_file);
                if (FileMatchesAllFilters(source_file, args_filter))
                {
                    continue;
                }
                var print_name = new NamePrinter(printable_file);
                if (first_file)
                {
                    if (false == short_args)
                    {
                        Printer.Header(project);
                    }
                    first_file = false;
                }
                if (args_nop is false)
                {
                    var (warnings, classes) = RunTidy(store, log, root, force, tidy_path, source_file, project_build_folder, stats, short_args, print_name, args_fix, printable_file, args_only);
                    if (short_args && warnings.TotalCount() > 0)
                    {
                        break;
                    }
                    project_counter.Update(warnings);
                    total_counter.Update(warnings);
                    total_classes.Update(classes);
                    foreach (var k in classes.Keys)
                    {
                        if (warnings_per_file.TryGetValue(k, out var warnings_list))
                        {
                            warnings_list.Add(printable_file);
                        }
                        else
                        {
                            warnings_per_file.Add(k, new() { printable_file });
                        }
                    }
                }
                else
                {
                    print_name.Print();
                }
            }

            if (!first_file && !short_args)
            {
                if (args_only.Length == 0)
                {
                    PrintWarningCounter(project_counter, project);
                    AnsiConsole.WriteLine("");
                    AnsiConsole.WriteLine("");
                }
            }
        }

        if (false == short_args && args_only.Length == 0)
        {
            Printer.Header("TIDY REPORT");
            PrintWarningCounter(total_counter, "total");
            AnsiConsole.WriteLine("");
            PrintWarningCounter(total_classes, "classes");
            AnsiConsole.WriteLine("");
            Printer.Line();
            AnsiConsole.WriteLine("");
            foreach (var (k, v) in warnings_per_file)
            {
                AnsiConsole.WriteLine($"{k}:");
                foreach (var f in v)
                {
                    AnsiConsole.WriteLine($"  {f}");
                }
                AnsiConsole.WriteLine("");
            }

            Printer.Line();
            AnsiConsole.WriteLine("");
            stats.Print();
        }

        if (total_counter.TotalCount() > 0)
        {
            return -1;
        }
        else
        {
            return 0;
        }
    }

    // callback function called when running clang.py format
    internal static int HandleClangFormatCommand(Log log, bool nop)
    {
        var root = Environment.CurrentDirectory;

        var data = MapAllFilesInRootOnFirstDir(root, FileUtil.HeaderAndSourceFiles);

        foreach (var (project, source_files) in data)
        {
            Printer.Header(project);
            foreach (var file in source_files)
            {
                AnsiConsole.WriteLine(Path.GetRelativePath(file, root));
                if (nop)
                {
                    continue;
                }

                var res = new ProcessBuilder("clang-format", "-i", file).RunAndGetOutput();
                if (res.ExitCode != 0)
                {
                    res.PrintOutput(log);
                    return -1;
                }
            }
            AnsiConsole.WriteLine("");
        }

        return 0;
    }
}
