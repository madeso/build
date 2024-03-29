using System.Collections.Concurrent;
using Spectre.Console;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Open.ChannelExtensions;
using Workbench.Config;
using Workbench.Shared;
using Workbench.Commands.Hero;
using static Workbench.Shared.Solution;

namespace Workbench.Commands.Clang;


internal class Store
{
    public readonly ConcurrentDictionary<Fil, StoredTidyUpdate> Cache = new();
}

internal record TidyOutput(string[] Output, TimeSpan Taken);
internal record StoredTidyUpdate(string[] Output, TimeSpan Taken, DateTime Modified);
internal record CategoryAndFiles(string Category, Fil[] Files);

internal class FileStatistics
{
    private readonly Dictionary<Fil, TimeSpan> data = new();

    internal void Add(Fil file, TimeSpan time)
    {
        data.Add(file, time);
    }

    internal void Print()
    {
        if (data.Count == 0) { return; }
        var average_value = TimeSpan.FromSeconds(data.Average(x => x.Value.TotalSeconds));
        var mi = data.MinBy(x => x.Value);
        var ma = data.MaxBy(x => x.Value);
        AnsiConsole.MarkupLineInterpolated($"average: {average_value}");
        AnsiConsole.MarkupLineInterpolated($"max: {ma.Value}s for {ma.Key}");
        AnsiConsole.MarkupLineInterpolated($"min: {mi.Value} for {mi.Key}");
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

internal static partial class ClangFacade
{
    private static Fil GetPathToStore(Dir build_folder)
        => build_folder.GetFile(FileNames.ClangTidyStore);

    private static Store? LoadStore(Log print, Dir build_folder)
    {
        var file_name = GetPathToStore(build_folder);
        if (!file_name.Exists)
        {
            return new Store();
        }

        var content = file_name.ReadAllText();
        var loaded = JsonUtil.Parse<Store>(print, file_name, content);
        return loaded;
    }

    private static void SaveStore(Dir build_folder, Store data)
    {
        var file_name = GetPathToStore(build_folder);
        file_name.WriteAllText(JsonUtil.Write(data));
    }

    private static bool IsFileIgnoredByClangTidy(Fil path)
    {
        var first_line = path.ReadAllLines().FirstOrDefault();
        if (first_line == null) { return false; }

        return first_line.StartsWith("// clang-tidy: ignore");
    }

    private static Fil GetPathToClangTidySource(Dir root)
        => root.GetFile("clang-tidy");

    // return a iterator over the the "compiled" .clang-tidy lines
    private static IEnumerable<string> GenerateClangTidyAsIterator(Dir root)
    {
        var clang_tidy_file = GetPathToClangTidySource(root).ReadAllLines();
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
    private static void PrintGeneratedClangTidy(Dir root)
    {
        foreach (var line in GenerateClangTidyAsIterator(root))
        {
            AnsiConsole.WriteLine(line);
        }
    }

    private static CategoryAndFiles[] MapFilesOnFirstDir(Dir root, IEnumerable<Fil> files_iterator)
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
                        .OrderBy(p => p.Name)
                        .ThenByDescending(p => p.Extension)
                        .ToArray()
                )
            ).ToArray();

    private static CategoryAndFiles[] MapAllFilesInRootOnFirstDir(Dir root, Func<Fil, bool> extension_filter)
        => MapFilesOnFirstDir(root, FileUtil.IterateFiles(root, false, true)
            .Where(extension_filter));

    private static bool FileMatchesAllFilters(Fil file, string[]? filters)
        => filters != null && filters.All(f => file.Path.Contains(f) == false);

    private static DateTime GetLastModification(Fil file)
        => file.LastWriteTimeUtc;

    private static DateTime GetLastModificationForFiles(IEnumerable<Fil> input_files)
     => input_files.Select(GetLastModification).Max();

    private static bool IsModificationTheLatest(IEnumerable<Fil> input_files, DateTime output)
        => GetLastModificationForFiles(input_files) <= output;

    private static TidyOutput? GetExistingOutputOrNull(Store store, Dir root, Fil source_file)
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

    private static void StoreOutput(Store store, Dir root, Dir project_build_folder,
        Fil source_file, TidyOutput output)
    {
        var clang_tidy_source = GetPathToClangTidySource(root);

        var data = new StoredTidyUpdate(output.Output, output.Taken,
            GetLastModificationForFiles(new[] { clang_tidy_source, source_file }));
        store.Cache[source_file] = data;
        SaveStore(project_build_folder, store);
    }

    // runs clang-tidy and returns all the text output
    private static async Task<TidyOutput> GetExistingOutputOrCallClangTidy(Store store,
        Log log, Dir root, bool force, Fil tidy_path, Dir project_build_folder,
        Fil source_file, bool fix)
    {
        if (false == force)
        {
            var existing_output = GetExistingOutputOrNull(store, root, source_file);
            if (existing_output != null)
            {
                return existing_output;
            }
        }

        var ret = await CallClangTidyAsync(log, tidy_path, project_build_folder, source_file, fix);
        StoreOutput(store, root, project_build_folder, source_file, ret);
        return ret;
    }

    private static async Task<TidyOutput> CallClangTidyAsync(Log log, Fil tidy_path,
        Dir project_build_folder, Fil source_file, bool fix)
    {
        var command = new ProcessBuilder(tidy_path);
        command.AddArgument("-p");
        command.AddArgument(project_build_folder.Path);
        if (fix)
        {
            command.AddArgument("--fix");
        }
        command.AddArgument(source_file.Path);

        var start = DateTime.Now;
        var output = await command.RunAndGetOutputAsync();

        if (output.ExitCode != 0)
        {
            log.Error($"Error: {output.ExitCode}");
            output.PrintOutput(log);
            // System.Exit(-1);
        }

        var end = DateTime.Now;
        var took = end - start;
        var ret = new TidyOutput(output.Output.Select(x => x.Line).ToArray(), took);
        return ret;
    }

    private static (ColCounter<Fil> warnings, ColCounter<string> classes, string[])
        CreateStatisticsAndPrintStatus(FileStatistics stats, bool short_list, Fil printable_file,
            string[] only, TidyOutput co)
    {
        var lines = new List<string>();

        var output = co.Output;
        var time_taken = co.Taken;
        var warnings = new ColCounter<Fil>();
        var classes = new ColCounter<string>();
        if (false == short_list && only.Length == 0)
        {
            AnsiConsole.WriteLine($"took {time_taken}");
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
                    var tidy_class = ClangTidyWarningClass().Match(line);
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
                        AnsiConsole.WriteLine(string.Empty);
                        print_empty = false;
                        lines.Add(string.Empty);
                    }
                }
                else
                {
                    if (false == hidden)
                    {
                        print_empty = true;
                        AnsiConsole.WriteLine(line);
                        lines.Add(line);
                    }
                }
            }
        }
        if (false == short_list && only.Length == 0)
        {
            PrintWarningCounter(classes, printable_file.GetDisplay(), c => c);
            AnsiConsole.WriteLine("");
        }
        return (warnings, classes, lines.ToArray());
    }

    // print warning counter to the console
    private static void PrintWarningCounter<T>(ColCounter<T> project_counter, string project, Func<T, string> display)
        where T: notnull
    {
        AnsiConsole.WriteLine($"{project_counter.TotalCount()} warnings in {project}.");
        foreach (var (file, count) in project_counter.MostCommon().Take(10))
        {
            AnsiConsole.WriteLine($"{display(file)} at {count}");
        }
    }

    [GeneratedRegex(@"\[(\w+([-,]\w+)+)\]", RegexOptions.Compiled)]
    private static partial Regex ClangTidyWarningClass();

    // ------------------------------------------------------------------------------

    // write the .clang-tidy from the clang-tidy "source"
    private static void WriteTidyFileToDisk(Dir root)
    {
        var content = GenerateClangTidyAsIterator(root);
        root.GetFile(".clang-tidy").WriteAllLines(content);
    }

    // callback function called when running clang.py make
    internal static void HandleMakeTidyCommand(bool nop)
    {
        var root = Dir.CurrentDirectory;
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
        var root = Dir.CurrentDirectory;

        var files = FileUtil.IterateFiles(root, false, true)
            .Where(FileUtil.IsSource);

        if (sort_files)
        {
            var sorted = MapFilesOnFirstDir(root, files);
            foreach (var (project, source_files) in sorted)
            {
                Printer.Header(project);
                foreach (var file in source_files)
                {
                    AnsiConsole.WriteLine(file.GetDisplay());
                }
                AnsiConsole.WriteLine("");
            }
        }
        else
        {
            foreach (var file in files)
            {
                AnsiConsole.WriteLine(file.GetDisplay());
            }
        }

        return 0;
    }

    // callback function called when running clang.py tidy
    internal static async Task<int> HandleRunClangTidyCommand(CompileCommandsArguments cc, Log log,
        bool force, bool headers, bool short_args, bool args_nop, string[] args_filter,
        string[] the_args_only, bool args_fix, int number_of_tasks)
    {
        var args_only = the_args_only.Where(x => string.IsNullOrWhiteSpace(x) == false).ToArray();

        var clang_tidy = Config.Paths.GetClangTidyExecutable(log);
        if (clang_tidy == null)
        {
            return -1;
        }

        var root = Dir.CurrentDirectory;
        var cc_file = CompileCommand.FindOrNone(cc, log);
        if (cc_file == null)
        {
            return -1;
        }

        var project_build_folder = cc_file.Directory;
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
        AnsiConsole.WriteLine($"using clang-tidy: {clang_tidy}");

        var total_counter = new ColCounter<Fil>();
        var total_classes = new ColCounter<string>();
        Dictionary<string, List<Fil>> warnings_per_file = new();

        var data = MapAllFilesInRootOnFirstDir(root, headers ? FileUtil.IsHeaderOrSource : FileUtil.IsSource);
        var stats = new FileStatistics();

        var errors = await PleaseRun(log, force, short_args, args_nop, args_filter, args_only,
            args_fix, data, store, root, clang_tidy, project_build_folder, stats, total_counter, total_classes, warnings_per_file, number_of_tasks);

        if (false == short_args && args_only.Length == 0)
        {
            Printer.Header("TIDY REPORT");
            foreach (var f in errors.OrderBy(x => x.File))
            {
                var panel = new Panel(Markup.Escape(string.Join(Environment.NewLine, f.Output)))
                {
                    Header = new PanelHeader(Markup.Escape(f.File.GetDisplay())),
                    Expand = true
                };
                AnsiConsole.Write(panel);
            }
            Printer.Line();
            PrintWarningCounter(total_counter, "total", f=> f.GetDisplay());
            AnsiConsole.WriteLine("");
            PrintWarningCounter(total_classes, "classes", c => c);
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

    private class CollectedTidyFil
    {
        public Fil File { get; }
        public string Category { get; }

        public CollectedTidyFil(Fil file, string category)
        {
            File = file;
            Category = category;
        }
    }

    private class FileWithError
    {
        public Fil File { get; }
        public string[] Output { get; }

        public FileWithError(Fil file, string[] output)
        {
            File = file;
            Output = output;
        }
    }
    private static async Task<FileWithError[]> PleaseRun(Log log, bool force, bool short_args, bool args_nop, string[] args_filter,
        string[] args_only, bool args_fix, CategoryAndFiles[] data, Store store, Dir root, Fil clang_tidy,
        Dir project_build_folder, FileStatistics stats, ColCounter<Fil> total_counter, ColCounter<string> total_classes,
        Dictionary<string, List<Fil>> warnings_per_file, int number_of_tasks)
    {
        var files = data.SelectMany(pair => pair.Files.Select(x => new CollectedTidyFil(x, pair.Category)))
            .Where(source_file => FileMatchesAllFilters(source_file.File, args_filter) == false);

        Dictionary<string, ColCounter<Fil>> pc = new();
        List<FileWithError> errors_to_print = new();

        await Channel
            .CreateUnbounded<CollectedTidyFil>()
            .Source(files)
            .PipeAsync(
                maxConcurrency: number_of_tasks,
                capacity: 100,
                transform: async source_file =>
                {
                    AnsiConsole.WriteLine($"Running {source_file.File}");
                    var co = args_nop
                        ? new(new string[]{}, new TimeSpan())
                        : await GetExistingOutputOrCallClangTidy(store, log, root, force, clang_tidy, project_build_folder, source_file.File, args_fix);
                    return (source_file.Category, source_file.File, co);
                })
            .ReadAll(tuple =>
            {
                var (cat, source_file, co) = tuple;
                AnsiConsole.WriteLine($"Collecting {source_file}");
                var (warnings, classes, lines) =
                    CreateStatisticsAndPrintStatus(stats, short_args, source_file, args_only, co);

                if (warnings.Items.Any())
                {
                    errors_to_print.Add(new FileWithError(source_file, lines));
                }

                if (false == pc.TryGetValue(cat, out var project_counter))
                {
                    project_counter = new();
                    pc[cat] = project_counter;
                }

                project_counter.Update(warnings);
                total_counter.Update(warnings);
                total_classes.Update(classes);
                foreach (var k in classes.Keys)
                {
                    // todo(Gustav): make this pattern nicer...
                    if (warnings_per_file.TryGetValue(k, out var warnings_list) == false)
                    {
                        warnings_list = new();
                        warnings_per_file.Add(k, warnings_list);
                    }

                    warnings_list.Add(source_file);
                }
            });


        if (!short_args && args_only.Length == 0)
        {
            foreach (var (project, warnings) in pc)
            {
                PrintWarningCounter(warnings, project, f => f.GetDisplay());
                AnsiConsole.WriteLine("");
                AnsiConsole.WriteLine("");
            }
        }

        return errors_to_print.ToArray();
    }

    // callback function called when running clang.py format
    internal static async Task<int> HandleClangFormatCommand(Log log, bool nop)
    {
        var clang_format_path = Config.Paths.GetClangFormatExecutable(log);
        if (clang_format_path == null)
        {
            return -1;
        }

        var root = Dir.CurrentDirectory;

        var data = MapAllFilesInRootOnFirstDir(root, FileUtil.IsHeaderOrSource);

        foreach (var (project, source_files) in data)
        {
            Printer.Header(project);
            foreach (var file in source_files)
            {
                AnsiConsole.WriteLine(file.GetRelative(root));
                if (nop)
                {
                    continue;
                }

                var res = await new ProcessBuilder(clang_format_path, "-i", file.Path)
                    .RunAndGetOutputAsync();
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
