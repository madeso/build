using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using static Workbench.Graphviz;

namespace Workbench.Clang;


internal class Store
{
    public readonly Dictionary<string, TidyStore> Cache = new();
}

internal class TidyOutput
{
    public string Output { get; init; }
    public TimeSpan Taken { get; init; }

    public TidyOutput(string output, TimeSpan taken)
    {
        Output = output;
        Taken = taken;
    }
}

internal class TidyStore
{
    public string Output { get; init; }
    public TimeSpan Taken { get; init; }
    public DateTime Modifed { get; init; }

    public TidyStore(string output, TimeSpan taken, DateTime modified)
    {
        Output = output;
        Taken = taken;
        Modifed = modified;
    }
}

internal class FileStatistics
{
    Dictionary<string, TimeSpan> data = new();

    internal void add(string file, TimeSpan time)
    {
        data.Add(file, time);
    }

    internal void print_data()
    {
        if(this.data.Count == 0) { return; }
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
    readonly string name;
    bool printed = false;

    public NamePrinter(string name)
    {
        this.name = name;
    }

    internal void print_name()
   {
        if(printed) { return; }

        AnsiConsole.WriteLine(this.name);
        this.printed = true;
   }
}

internal static class F
{
    private static string path_to_output_store(string build_folder)
    {
        return Path.Join(build_folder, "clang-tidy-store.json");
    }

    private static Store? get_file_data(Printer print, string file_name, Store missing_file)
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

    private static Store? get_store(Printer print, string build_folder)
    {
        return get_file_data(print, path_to_output_store(build_folder), new Store());
    }

    private static void set_file_data(string file_name, Store data)
    {
        File.WriteAllText(file_name, JsonUtil.Write(data));
    }

    private static bool is_file_ignored(string path)
    {
        var lines = File.ReadAllLines(path);
        if(lines.Length == 0) { return false; }

        var firstLine = lines[0];
        return firstLine.StartsWith("// clang-tidy: ignore");
    }

    private static IEnumerable<string> list_files_in_folder(string path, string[] extensions)
    {
        foreach(var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(f);
            if(extensions.Contains(ext))
            {
                yield return f;
            }
        }
    }

    private static string clang_tidy_root(string root)
    {
        return Path.Join(root, "clang-tidy");
    }

    // return a iterator over the the "compiled" .clang-tidy lines
    private static IEnumerable<string> clang_tidy_lines(string root)
    {
        var clang_tidy_file = File.ReadAllLines(clang_tidy_root(root));
        var write = false;
        var checks = new List<string>();
        foreach(var line in clang_tidy_file)
        {
            if(write)
            {
                var l = line.TrimEnd();
                if(false == l.TrimStart().StartsWith("//"))
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
    private static void print_clang_tidy_source(string root)
    {
        foreach (var line in clang_tidy_lines(root))
        {
            AnsiConsole.WriteLine(line);
        }
    }


    private static Dictionary<string, List<string>> sort_and_map_files(string root, IEnumerable<string> iterator_files)
    {
        var ret = new Dictionary<string, List<string>>();
        var files = iterator_files
            .OrderByDescending(x => Path.GetExtension(x))
            .OrderBy(x => Path.GetFileName(x));
        foreach(var file in files)
        {
            var rel = Path.GetRelativePath(root, file);
            // ignore external folder
            // ignore build folder
            if (rel.StartsWith("external") || rel.StartsWith("build"))
            {
                // pass
            }
            else if(false == is_file_ignored(file))
            {
                var cat = rel.Split(Path.DirectorySeparatorChar, 2)[0];
                if(ret.TryGetValue(cat, out var values))
                {
                    values.Add(file);
                }
                else
                {
                    ret.Add(cat, new(){ file });
                }
            }
        }

        return ret;
    }

    private static Dictionary<string, List<string>> extract_data_from_root(string root, string[] files)
    {
        return sort_and_map_files(root, list_files_in_folder(root, files));
    }

    private static bool filter_out_file(string[]? filters, string file)
    {
        if(filters == null)
        {
            return false;
        }

        return filters.All(f => file.Contains(f) == false);
    }

    private static DateTime file_time(string file)
    {
        var info = new FileInfo(file);
        return info.LastWriteTimeUtc;
    }

    private static DateTime get_last_modification(string[] input_files)
    {
        return input_files.Select(x => file_time(x)).Max();
    }

    private static bool is_all_up_to_date(string[] input_files, DateTime output)
    {
        var sourcemod = get_last_modification(input_files);
        return sourcemod <= output;
    }

    private static TidyOutput? get_existing_output(Store store, Printer printer, string root, string project_build_folder, string source_file)
    {
        var root_file = clang_tidy_root(root);

        if(store.Cache.TryGetValue(source_file, out var stored) == false)
        {
            return null;
        }
        
        if(is_all_up_to_date(new string[] { root_file, source_file }, stored.Modifed))
        {
            return new TidyOutput(stored.Output, stored.Taken);
        }

        return null;
    }

    private static void set_existing_output(Store store, Printer printer, string root, string project_build_folder, string source_file, string existing_output, TimeSpan time)
    {
        var root_file = clang_tidy_root(root);
        var data = new TidyStore(existing_output, time, get_last_modification(new string[] { root_file, source_file }));
        store.Cache[source_file] = data;
        set_file_data(path_to_output_store(project_build_folder), store);
    }

    // runs clang-tidy and returns all the text output
    private static TidyOutput call_clang_tidy(Store store, Printer printer, string root, bool force, string tidy_path, string project_build_folder, string source_file, NamePrinter name_printer, bool fix)
    {
        if(false == force)
        {
            var existing_output = get_existing_output(store, printer, root, project_build_folder, source_file);
            if(existing_output != null)
            {
                return existing_output;
            }
        }

        var command = new Command(tidy_path);
        command.arg("-p");
        command.arg(project_build_folder);
        if(fix)
        {
            command.arg("--fix");
        }
        command.arg(source_file);
        
        name_printer.print_name();
        var start = new DateTime();
        var output = command.wait_for_exit(printer);

        if(output.ExitCode != 0)
        {
            printer.error($"Error: {output.ExitCode}");
            output.Print(printer);
            // System.Exit(-1);
        }

        var end = new DateTime();
        var took = end - start;
        set_existing_output(store, printer, root, project_build_folder, source_file, output.stdout, took);
        return new TidyOutput(output.stdout, took);
    }

    // runs the clang-tidy process, printing status to terminal
    private static (Workbench.ColCounter<string> warnings, Workbench.ColCounter<string> classes) run_clang_tidy(Store store, Printer printer, string root, bool force, string tidy_path, string source_file, string project_build_folder, FileStatistics stats, bool shortList, NamePrinter name_printer, bool fix, string printable_file, string[] only)
    {
        var co = call_clang_tidy(store, printer, root, force, tidy_path, project_build_folder, source_file, name_printer, fix);
        var output = co.Output;
        var time_taken = co.Taken;
        var warnings = new ColCounter<string>();
        var classes = new ColCounter<string>();
        if(false== shortList && only.Length == 0)
        {
            name_printer.print_name();
            printer.info($"took {time_taken:.2f}s");
        }
        stats.add(printable_file, time_taken);
        var print_empty = false;
        var hidden = only.Length > 0;
        foreach(var line in output.Split("\n"))
        {
            if(line.Contains("warnings generated"))
            {
                // pass;
            }
            else if(line.Contains("Use -header-filter=.* to display errors"))
            {
                // pass
            }
            else if(line.Contains("Suppressed") && line.Contains("NOLINT)."))
            {
                // pass
            }
            else if(line.Contains("Suppressed") && line.Contains("non-user code"))
            {
                // pass
            }
            else
            {
                if(line.Contains("warning: "))
                {
                    warnings.AddOne(printable_file);
                    var tidy_class = CLANG_TIDY_WARNING_CLASS.Match(line);
                    if(tidy_class.Success)
                    {
                        var warning_classes = tidy_class.Groups[1];
                        foreach(var warning_class in warning_classes.Value.Split(','))
                        {
                            classes.AddOne(warning_class);
                            hidden = only.Length > 0;
                            if(only.Contains(warning_class))
                            {
                                hidden = false;
                            }
                        }
                    }
                }
                if(line.Trim() == "")
                {
                    if(false == hidden && print_empty)
                    {
                        printer.info("");
                        print_empty = false;
                    }
                }
                else
                {
                    if(false == hidden)
                    {
                        print_empty = true;
                        printer.info(line);
                    }
                }
            }
        }
        if(false == shortList && only.Length == 0)
        {
            print_warning_counter(printer, classes, printable_file);
            printer.info("");
        }
        return (warnings, classes);
    }

    // print warning counter to the console
    private static void print_warning_counter(Printer print, ColCounter<string> project_counter, string project)
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
    private static void make_clang_tidy(string root)
    {
        var content = clang_tidy_lines(root).ToArray();
        File.WriteAllLines(Path.Join(root, ".clang-tidy"), content);
    }

    // callback function called when running clang.py make
    internal static void handle_make_tidy(bool nop)
    {
        var root = Environment.CurrentDirectory;
        if(nop)
        {
            print_clang_tidy_source(root);
        }
        else
        {
            make_clang_tidy(root);
        }
    }

    static readonly string[] HEADER_FILES = new string[]{"", ".h", ".hpp", ".hxx"};
    static readonly string[] SOURCE_FILES = new string[]{".cc", ".cpp", ".cxx", ".inl"};

    internal static int handle_list(Printer print, bool args_sort)
    {
        var root = Environment.CurrentDirectory;

        var project_build_folder = CompileCommands.Utils.find_build_root(root);
        if(project_build_folder is null)
        {
            print.error("unable to find build folder");
            return -1;
        }

        var files = list_files_in_folder(root, SOURCE_FILES);

        if(args_sort)
        {
            var sorted = sort_and_map_files(root, files);
            foreach(var (project, source_files) in sorted)
            {
                print.header(project);
                foreach(var source_file in source_files)
                {
                    print.info(source_file);
                }
                print.info("");
            }
        }
        else
        {
            foreach(var file in files)
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

        var store = get_store(printe, project_build_folder);
        if(store == null)
        {
            printe.error("unable to find load store");
            return -1;
        }

        make_clang_tidy(root);
        printe.info($"using clang-tidy: {tidy_path}");

        var total_counter = new ColCounter<string>();
        var total_classes = new ColCounter<string>();
        Dictionary<string, List<string>> warnings_per_file = new();

        var data = extract_data_from_root(root, headers ? SOURCE_FILES.Concat(HEADER_FILES).ToArray() : SOURCE_FILES);
        var stats = new FileStatistics();

        foreach(var (project, source_files) in data)
        {
            var first_file = true;
            var project_counter = new ColCounter<string>();
            foreach(var source_file in source_files)
            {
                var printable_file = Path.GetRelativePath(root, source_file);
                if(filter_out_file(args_filter, source_file))
                {
                    continue;
                }
                var print_name = new NamePrinter(printable_file);
                if(first_file)
                {
                    if(false == short_args)
                    {
                        printe.header(project);
                    }
                    first_file = false;
                }
                if(args_nop is false)
                {
                    var (warnings, classes) = run_clang_tidy(store, printe, root, force, tidy_path, source_file, project_build_folder, stats, short_args, print_name, args_fix, printable_file, args_only);
                    if(short_args && warnings.TotalCount() > 0)
                    {
                        break;
                    }
                    project_counter.update(warnings);
                    total_counter.update(warnings);
                    total_classes.update(classes);
                    foreach(var k in classes.Keys)
                    {
                        if(warnings_per_file.TryGetValue(k, out var warnings_list))
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
                    print_name.print_name();
                }
            }

            if(!first_file && !short_args)
            {
                if(args_only.Length == 0)
                {
                    print_warning_counter(printe, project_counter, project);
                    printe.info("");
                    printe.info("");
                }
            }
        }

        if(false == short_args && args_only.Length == 0)
        {
            printe.header("TIDY REPORT");
            print_warning_counter(printe, total_counter, "total");
            printe.info("");
            print_warning_counter(printe, total_classes, "classes");
            printe.info("");
            printe.line();
            printe.info("");
            foreach(var (k,v) in warnings_per_file)
            {
                printe.info($"{k}:");
                foreach(var f in v)
                {
                    printe.info($"  {f}");
                }
                printe.info("");
            }

            printe.line();
            printe.info("");
            stats.print_data();
        }

        if(total_counter.TotalCount() > 0)
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

        var data = extract_data_from_root(root, SOURCE_FILES.Concat(HEADER_FILES).ToArray());

        foreach(var (project, source_files) in data)
        {
            printer.header(project);
            foreach(var source_file in source_files)
            {
                printer.info(Path.GetRelativePath(source_file, root));
                if(args_nop == false)
                {
                    var res = new Command("clang-format", "-i", source_file).wait_for_exit(printer);
                    if(res.ExitCode != 0)
                    {
                        res.Print(printer);
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
        F.handle_make_tidy(settings.Nop);
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
        return CommonExecute.WithPrinter(print => F.handle_list(print, settings.Sort));
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
        public string[]? Only {get; set;}

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
