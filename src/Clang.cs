using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Workbench.Clang;


internal class Store
{
    public readonly Dictionary<string, StoredTidyUpdate> Cache = new();
}

internal class TidyOutput
{
    public string[] Output { get; init; }
    public TimeSpan Taken { get; init; }

    public TidyOutput(string[] output, TimeSpan taken)
    {
        Output = output;
        Taken = taken;
    }
}

internal class StoredTidyUpdate
{
    public string[] Output { get; init; }
    public TimeSpan Taken { get; init; }
    public DateTime Modifed { get; init; }

    public StoredTidyUpdate(string[] output, TimeSpan taken, DateTime modified)
    {
        Output = output;
        Taken = taken;
        Modifed = modified;
    }
}

internal class FileStatistics
{
    private readonly Dictionary<string, TimeSpan> data = new();

    internal void Add(string file, TimeSpan time)
    {
        data.Add(file, time);
    }

    internal void Print()
    {
        if (this.data.Count == 0) { return; }
        var average_value = TimeSpan.FromSeconds(data.Average(x => x.Value.TotalSeconds));
        var mi = data.MinBy(x => x.Value);
        var ma = data.MaxBy(x => x.Value);
        AnsiConsole.MarkupLine($"average: {average_value:.2f}s");
        AnsiConsole.MarkupLine($"max: {ma.Value:.2f}s foreach {ma.Key}");
        AnsiConsole.MarkupLine($"min: {mi.Value:.2f}s foreach {mi.Key}");
        AnsiConsole.MarkupLine($"{data.Count} files");
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

        AnsiConsole.WriteLine(this.name);
        this.printed = true;
    }
}

internal static class F
{
    private static string GetPathToStore(string build_folder)
    {
        return Path.Join(build_folder, "clang-tidy-store.json");
    }

    private static Store? LoadStore(Printer print, string buildFolder)
    {
        static Store? get_file_data(Printer print, string file_name, Store missing_file)
        {
            if (File.Exists(file_name))
            {
                var content = File.ReadAllText(file_name);
                var loaded = JsonUtil.Parse<Store>(print, file_name, content);
                return loaded;
            }
            else
            {
                return missing_file;
            }
        }

        return get_file_data(print, GetPathToStore(buildFolder), new Store());
    }

    private static void SaveStore(string buildFolder, Store data)
    {
        var fileName = GetPathToStore(buildFolder);
        File.WriteAllText(fileName, JsonUtil.Write(data));
    }

    private static bool IsFileIgnoredByClangTidy(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length == 0) { return false; }

        var firstLine = lines[0];
        return firstLine.StartsWith("// clang-tidy: ignore");
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

    private static CategoryAndFiles[] MapFilesOnFirstDir(string root, IEnumerable<string> filesIterator)
    {
        var ret = filesIterator
            .Select(f => new { Path = f, Cat = FileUtil.GetFirstFolder(root, f) })
            // ignore external and build folders
            .Where(f => f.Cat != "external").Where(f => f.Cat.StartsWith("build") == false)
            // permform actual grouping
            .GroupBy
            (
                x => x.Cat,
                (cat, files) => new CategoryAndFiles(
                    cat,
                    files
                        // sort files
                        .Select(x => x.Path)
                        .OrderBy(f => Path.GetFileName(f))
                        .ThenByDescending(f => Path.GetExtension(f))
                        .ToArray()
                )
            ).ToArray();
        return ret;
    }

    private static CategoryAndFiles[] MapAllFilesInRootOnFirstDir(string root, string[] extensions)
    {
        return MapFilesOnFirstDir(root, FileUtil.ListFilesRecursivly(root, extensions));
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

    private static DateTime GetLastModificationForFiles(string[] input_files)
    {
        return input_files.Select(x => GetLastModification(x)).Max();
    }

    private static bool IsModificationTheLatest(string[] input_files, DateTime output)
    {
        var sourcemod = GetLastModificationForFiles(input_files);
        return sourcemod <= output;
    }

    private static TidyOutput? GetExistingOutputOrNull(Store store, Printer printer, string root, string project_build_folder, string source_file)
    {
        var root_file = GetPathToClangTidySource(root);

        if (store.Cache.TryGetValue(source_file, out var stored) == false)
        {
            return null;
        }

        if (IsModificationTheLatest(new string[] { root_file, source_file }, stored.Modifed))
        {
            return new TidyOutput(stored.Output, stored.Taken);
        }

        return null;
    }

    private static void StoreOutput(Store store, Printer printer, string root, string project_build_folder, string sourceFile, TidyOutput output)
    {
        var clangTidySource = GetPathToClangTidySource(root);

        var data = new StoredTidyUpdate(output.Output, output.Taken, GetLastModificationForFiles(new string[] { clangTidySource, sourceFile }));
        store.Cache[sourceFile] = data;
        SaveStore(project_build_folder, store);
    }

    // runs clang-tidy and returns all the text output
    private static TidyOutput GetExistingOutputOrCallClangTidy(Store store, Printer printer, string root, bool force, string tidy_path, string project_build_folder, string source_file, bool fix)
    {
        if (false == force)
        {
            var existing_output = GetExistingOutputOrNull(store, printer, root, project_build_folder, source_file);
            if (existing_output != null)
            {
                return existing_output;
            }
        }

        TidyOutput ret = CallClangTidy(printer, tidy_path, project_build_folder, source_file, fix);
        StoreOutput(store, printer, root, project_build_folder, source_file, ret);
        return ret;
    }

    private static TidyOutput CallClangTidy(Printer printer, string tidy_path, string project_build_folder, string source_file, bool fix)
    {
        var command = new Command(tidy_path);
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
            printer.error($"Error: {output.ExitCode}");
            output.PrintOutput(printer);
            // System.Exit(-1);
        }

        var end = new DateTime();
        var took = end - start;
        var ret = new TidyOutput(output.Output, took);
        return ret;
    }

    // runs the clang-tidy process, printing status to terminal
    private static (ColCounter<string> warnings, ColCounter<string> classes) RunTidy(Store store, Printer printer, string root, bool force, string tidy_path, string source_file, string project_build_folder, FileStatistics stats, bool shortList, NamePrinter name_printer, bool fix, string printable_file, string[] only)
    {
        name_printer.Print();
        var co = GetExistingOutputOrCallClangTidy(store, printer, root, force, tidy_path, project_build_folder, source_file, fix);
        return CreateStatisticsAndPrintStatus(printer, stats, shortList, printable_file, only, co);
    }

    private static (ColCounter<string> warnings, ColCounter<string> classes) CreateStatisticsAndPrintStatus(Printer printer, FileStatistics stats, bool shortList, string printable_file, string[] only, TidyOutput co)
    {
        var output = co.Output;
        var time_taken = co.Taken;
        var warnings = new ColCounter<string>();
        var classes = new ColCounter<string>();
        if (false == shortList && only.Length == 0)
        {
            printer.info($"took {time_taken:.2f}s");
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
                        printer.info("");
                        print_empty = false;
                    }
                }
                else
                {
                    if (false == hidden)
                    {
                        print_empty = true;
                        printer.info(line);
                    }
                }
            }
        }
        if (false == shortList && only.Length == 0)
        {
            PrintWarningCounter(printer, classes, printable_file);
            printer.info("");
        }
        return (warnings, classes);
    }

    // print warning counter to the console
    private static void PrintWarningCounter(Printer print, ColCounter<string> project_counter, string project)
    {
        print.info($"{project_counter.TotalCount()} warnings in {project}.");
        foreach (var (file, count) in project_counter.MostCommon().Take(10))
        {
            print.info($"{file} at {count}");
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

    internal static int HandleTidyListCommand(Printer print, bool args_sort)
    {
        var root = Environment.CurrentDirectory;

        var project_build_folder = CompileCommands.Utils.find_build_root(root);
        if (project_build_folder is null)
        {
            print.error("unable to find build folder");
            return -1;
        }

        var files = FileUtil.ListFilesRecursivly(root, Workbench.FileUtil.SOURCE_FILES);

        if (args_sort)
        {
            var sorted = MapFilesOnFirstDir(root, files);
            foreach (var (project, source_files) in sorted)
            {
                print.header(project);
                foreach (var source_file in source_files)
                {
                    print.info(source_file);
                }
                print.info("");
            }
        }
        else
        {
            foreach (var file in files)
            {
                print.info(file);
            }
        }

        return 0;
    }

    // callback function called when running clang.py tidy
    internal static int handle_tidy(Printer printe, string tidy_path, bool force, bool headers, bool short_args, bool args_nop, string[] args_filter, string[] args_only, bool args_fix)
    {
        var root = Environment.CurrentDirectory;
        var project_build_folder = CompileCommands.Utils.find_build_root(root);
        if (project_build_folder is null)
        {
            printe.error("unable to find build folder");
            return -1;
        }

        var store = LoadStore(printe, project_build_folder);
        if (store == null)
        {
            printe.error("unable to find load store");
            return -1;
        }

        WriteTidyFileToDisk(root);
        printe.info($"using clang-tidy: {tidy_path}");

        var total_counter = new ColCounter<string>();
        var total_classes = new ColCounter<string>();
        Dictionary<string, List<string>> warnings_per_file = new();

        var data = MapAllFilesInRootOnFirstDir(root, headers ? Workbench.FileUtil.SOURCE_FILES.Concat(Workbench.FileUtil.HEADER_FILES).ToArray() : Workbench.FileUtil.SOURCE_FILES);
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
                        printe.header(project);
                    }
                    first_file = false;
                }
                if (args_nop is false)
                {
                    var (warnings, classes) = RunTidy(store, printe, root, force, tidy_path, source_file, project_build_folder, stats, short_args, print_name, args_fix, printable_file, args_only);
                    if (short_args && warnings.TotalCount() > 0)
                    {
                        break;
                    }
                    project_counter.update(warnings);
                    total_counter.update(warnings);
                    total_classes.update(classes);
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
                    PrintWarningCounter(printe, project_counter, project);
                    printe.info("");
                    printe.info("");
                }
            }
        }

        if (false == short_args && args_only.Length == 0)
        {
            printe.header("TIDY REPORT");
            PrintWarningCounter(printe, total_counter, "total");
            printe.info("");
            PrintWarningCounter(printe, total_classes, "classes");
            printe.info("");
            printe.line();
            printe.info("");
            foreach (var (k, v) in warnings_per_file)
            {
                printe.info($"{k}:");
                foreach (var f in v)
                {
                    printe.info($"  {f}");
                }
                printe.info("");
            }

            printe.line();
            printe.info("");
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
    internal static int handle_format(Printer printer, bool args_nop)
    {
        var root = Environment.CurrentDirectory;

        var project_build_folder = CompileCommands.Utils.find_build_root(root);
        if (project_build_folder is null)
        {
            printer.error("unable to find build folder");
            return -1;
        }

        var data = MapAllFilesInRootOnFirstDir(root, Workbench.FileUtil.SOURCE_FILES.Concat(Workbench.FileUtil.HEADER_FILES).ToArray());

        foreach (var (project, source_files) in data)
        {
            printer.header(project);
            foreach (var source_file in source_files)
            {
                printer.info(Path.GetRelativePath(source_file, root));
                if (args_nop == false)
                {
                    var res = new Command("clang-format", "-i", source_file).RunAndGetOutput();
                    if (res.ExitCode != 0)
                    {
                        res.PrintOutput(printer);
                        return -1;
                    }
                }
            }
            printer.info("");
        }

        return 0;
    }
}


internal sealed class MakeCommand : Command<MakeCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("don't write anything")]
        [CommandOption("--nop")]
        [DefaultValue(false)]
        public bool Nop { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        F.HandleMakeTidyCommand(settings.Nop);
        return 0;
    }
}

internal sealed class ListCommand : Command<ListCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("sort listing")]
        [CommandOption("--sort")]
        [DefaultValue(false)]
        public bool Sort { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print => F.HandleTidyListCommand(print, settings.Sort));
    }
}

internal sealed class TidyCommand : Command<TidyCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("combinatory filter on what to run")]
        [CommandArgument(0, "<filter>")]
        public string[] Filter { get; set; } = new string[0];

        [Description("don't do anything")]
        [CommandOption("--nop")]
        [DefaultValue(false)]
        public bool Nop { get; set; }

        [Description("try to fix the source")]
        [CommandOption("--fix")]
        [DefaultValue(false)]
        public bool Fix { get; set; }

        [Description("use shorter and stop after one file")]
        [CommandOption("--short")]
        [DefaultValue(false)]
        public bool Short { get; set; }

        [Description("also list files in the summary")]
        [CommandOption("--list")]
        [DefaultValue(false)]
        public bool List { get; set; }

        [Description("don't tidy headers")]
        [CommandOption("--no-headers")]
        [DefaultValue(false)]
        public bool Headers { get; set; }

        [Description("Force clang-tidy to run, even if there is a result")]
        [CommandOption("--force")]
        [DefaultValue(true)]
        public bool Force { get; set; }

        // [Description("try to fix the source")]
        [CommandOption("--only")]
        [DefaultValue(null)]
        public string[]? Only { get; set; }

        [Description("the clang-tidy to use")]
        [CommandOption("--tidy")]
        [DefaultValue("clang-tidy")]
        public string ClangTidy { get; set; } = string.Empty;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print => F.handle_tidy(
            print,
            settings.ClangTidy,
            settings.Force,
            settings.Headers,
            settings.Short,
            settings.Nop,
            settings.Filter,
            settings.Only ?? Array.Empty<string>(),
            settings.Fix));
    }
}

internal sealed class FormatCommand : Command<FormatCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("don't format anything")]
        [CommandOption("--nop")]
        [DefaultValue(false)]
        public bool Nop { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print => F.handle_format(print, settings.Nop));
    }
}

public static class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, git =>
        {
            git.SetDescription("clang-tidy and clang-format related tools");
            git.AddCommand<MakeCommand>("make").WithDescription("make .clang-tidy");
            git.AddCommand<ListCommand>("ls").WithDescription("list files");
            git.AddCommand<TidyCommand>("tidy").WithDescription("do clang tidy on files");
            git.AddCommand<FormatCommand>("format").WithDescription("do clang format on files");
        });
    }
}

internal record CategoryAndFiles(string Category, string[] Files);
