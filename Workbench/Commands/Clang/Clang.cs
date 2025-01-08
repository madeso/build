using System.Collections.Immutable;
using System.Collections.Concurrent;
using Spectre.Console;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Open.ChannelExtensions;
using Workbench.Config;
using Workbench.Shared;
using static Workbench.Commands.Clang.ClangTidy;
using System.Xml.Linq;

namespace Workbench.Commands.Clang;


public class Store
{
    public ConcurrentDictionary<Fil, StoredTidyUpdate> Cache { get; set; }

    public Store()
    {
        this.Cache = new();
    }

    public Store(JsonStore s)
    {
        this.Cache = new(s.Cache.Select(x => new KeyValuePair<Fil, StoredTidyUpdate>(x.File, x.Output)));
    }
}

public class JsonCacheEntry
{
    [JsonPropertyName("file")]
    public Fil File { get; set; }

    [JsonPropertyName("output")]
    public StoredTidyUpdate Output { get; set; }

    public JsonCacheEntry()
    {
        this.File = Dir.CurrentDirectory.GetFile("missing.txt");
        this.Output = new(new string[] { }, TimeSpan.Zero, DateTime.Now);
    }
    public JsonCacheEntry(Fil f, StoredTidyUpdate o)
    {
        this.File = f;
        this.Output = o;
    }
}

public class JsonStore
{
    [JsonPropertyName("cache")]
    public List<JsonCacheEntry> Cache { get; set; }

    public JsonStore()
    {
        Cache = new();
    }

    public JsonStore(Store s)
    {
        this.Cache = new(s.Cache.Select(x => new JsonCacheEntry(x.Key, x.Value)));
    }
}

public record TidyOutput(string[] Output, TimeSpan Taken);
public class StoredTidyUpdate
{
    [JsonPropertyName("output")]
    public string[] Output { get; set; }

    [JsonPropertyName("taken")]
    public TimeSpan Taken { get; set; }

    [JsonPropertyName("modified")]
    public DateTime Modified { get; set; }

    public StoredTidyUpdate(string[] output, TimeSpan taken, DateTime modified)
    {
        this.Output = output;
        this.Taken = taken;
        this.Modified = modified;
    }
}

public record CategoryAndFiles(string Category, Fil[] Files);

internal class FileWithError
{
    public Fil File { get; }
    private string[] Output { get; }

    public FileWithError(Fil file, string[] output)
    {
        File = file;
        Output = output;
    }
}

internal record TimeTaken(TimeSpan average_value, KeyValuePair<Fil, TimeSpan> mi, KeyValuePair<Fil, TimeSpan> ma, int times_per_file_count);

internal class GlobalStatistics
{
    private readonly Dictionary<Fil, TimeSpan> times_per_file = new();

    public Dictionary<string, ColCounter<Fil>> ProjectCounters { get; } = new();

    public Dictionary<string, List<Fil>> WarningsPerFile { get; } = new();

    public ColCounter<Fil> TotalCounter { get; } = new();
    public ColCounter<string> TotalClasses { get; } = new();

    // public List<FileWithError> Errors { get; } = new();

    internal void AddTimeTaken(Fil file, TimeSpan time)
    {
        times_per_file.Add(file, time);
    }

    internal TimeTaken? GetTimeTaken()
    {
        if (times_per_file.Count == 0) { return null; }
        var average_value = TimeSpan.FromSeconds(times_per_file.Average(x => x.Value.TotalSeconds));
        var mi = times_per_file.MinBy(x => x.Value);
        var ma = times_per_file.MaxBy(x => x.Value);
        return new TimeTaken(average_value, mi, ma, times_per_file.Count);
    }

    public ColCounter<Fil> GetProjectCounter(string cat)
    {
        if (ProjectCounters.TryGetValue(cat, out var project_counter)) return project_counter;

        project_counter = new();
        ProjectCounters[cat] = project_counter;
        return project_counter;
    }

    public List<Fil> GetWarningsList(string k)
    {
        if (WarningsPerFile.TryGetValue(k, out var warnings_list)) return warnings_list;

        warnings_list = new();
        WarningsPerFile.Add(k, warnings_list);
        return warnings_list;
    }
}

internal class FileStats
{
    public ColCounter<Fil> Warnings { get; } = new();
    public ColCounter<string> Classes { get; } = new();
}

class TidyMessage(Fil a_fil)
{
    public Fil File { get; } = a_fil;
    public int Line { get; set; } = 0;
    public int Column { get; set; } = 0;
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Category { get; set; } = null;
    public List<string> Code { get; set; } = new();

    public static IEnumerable<TidyMessage> Parse(IEnumerable<string> lines)
    {
        var reg = new Regex(@"(?<file>[^:]+):(?<line>[0-9]+):(?<col>[0-9]+): (?<type>[^$]+):(?<mess>[^$]+)");
        TidyMessage? current = null;
        foreach (var l in lines)
        {
            if (string.IsNullOrEmpty(l)) continue;

            var is_code = l.TrimStart() != l; // does the line start with space?
            if (is_code)
            {
                if (current == null)
                {
                    Console.WriteLine($"WARNING: Unexpected code line: '{l}'");
                    continue;
                }
                current.Code.Add(l);
            }
            else
            {
                if (current != null) yield return current;

                var l2 = l;

                var tidy_class = ClangTIdyParsing.ClangTidyWarningClass().Match(l2);
                if (tidy_class.Success)
                {
                    l2 = l2.Substring(0, tidy_class.Index).Trim();
                }

                var parsed = reg.Match(l2);
                if (parsed.Success == false)
                {
                    Console.WriteLine($"WARNING: Invalid line: '{l}'");
                    current = null;
                    continue;
                }
                string? cat = null;
                if (tidy_class.Success) cat = tidy_class.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(cat)) cat = null;
                current = new TidyMessage(new Fil(parsed.Groups["file"].Value.Trim()))
                {
                    Line = int.Parse(parsed.Groups["line"].Value),
                    Column = int.Parse(parsed.Groups["col"].Value),
                    Type = parsed.Groups["type"].Value.Trim(),
                    Message = parsed.Groups["mess"].Value.Trim(),
                    Category = cat,
                };
            }
        }

        if (current != null) yield return current;
    }

    public IEnumerable<string> GetClasses()
        => Category!.Split(',').Select(s => s.Trim());
}

class TidyGroup
{
    public List<TidyMessage> messages { get; set; } = new();

    public static IEnumerable<TidyGroup> Parse(IEnumerable<string> lines)
    {
        TidyGroup? current = null;
        foreach (var mess in TidyMessage.Parse(lines))
        {
            if (current == null || mess.Type != "note")
            {
                if (current != null) yield return current;
                current = new TidyGroup();
            }
            current.messages.Add(mess);
        }

        if (current != null)
        {
            yield return current;
        }
    }
}


internal static class ClangTidyFile
{
    internal static Fil GetPathToClangTidySource(Dir root)
        => root.GetFile("clang-tidy");

    // return a iterator over the "compiled" .clang-tidy lines
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

    // write the .clang-tidy from the clang-tidy "source"
    internal static void WriteTidyFileToDisk(Dir root)
    {
        var content = GenerateClangTidyAsIterator(root);
        root.GetFile(".clang-tidy").WriteAllLines(content);
    }

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
}

public enum FileSection
{
    AllFiles, AllExceptThoseIgnoredByClangTidy
}

internal static class ClangFiles
{
    private static bool IsFileIgnoredByClangTidy(Fil path)
    {
        var first_line = path.ReadAllLines().FirstOrDefault();
        if (first_line == null) { return false; }

        return first_line.StartsWith("// clang-tidy: ignore");
    }

    private static CategoryAndFiles[] MapFilesOnFirstDir(Dir root, FileSection fs, IEnumerable<Fil> files_iterator)
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
                        .Where(f => fs == FileSection.AllFiles || !IsFileIgnoredByClangTidy(f))
                        .OrderBy(p => p.Name)
                        .ThenByDescending(p => p.Extension)
                        .ToArray()
                )
            ).ToArray();

    internal static CategoryAndFiles[] MapAllFilesInRootOnFirstDir(Dir root, Func<Fil, bool> extension_filter, FileSection fs)
        => MapFilesOnFirstDir(root, fs, FileUtil.FilesInPitchfork(root, false)
            .Where(extension_filter));

    internal static int HandleTidyListFilesCommand(Log print, bool sort_files, FileSection fs)
    {
        var root = Dir.CurrentDirectory;

        var files = FileUtil.IterateFiles(root, false, true)
            .Where(FileUtil.IsSource);

        if (sort_files)
        {
            var sorted = MapFilesOnFirstDir(root, fs, files);
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
}

internal static partial class ClangTIdyParsing
{
    [GeneratedRegex(@"\[(\w+([-,]\w+)+)\]", RegexOptions.Compiled)]
    public static partial Regex ClangTidyWarningClass();
}

internal class SingleFileReport
{
    public SingleFileReport(TimeSpan taken, IEnumerable<string> lines, FileStats file_stats)
    {
        Messages = [.. lines];
        GroupedMessages = [.. TidyGroup.Parse(Messages)];
        TimeTaken = taken;
        Stats = file_stats;
    }

    public TimeSpan TimeTaken { get; }
    public ImmutableArray<string> Messages { get; }
    public ImmutableArray<TidyGroup> GroupedMessages { get; }
    public FileStats Stats;
}

internal interface IOutput
{
    void WriteFinalReport(GlobalStatistics stats);
    void SingleFileReport(Fil source_file, SingleFileReport report);
}

internal class ConsoleOutput(Args args) : IOutput
{
    // print warning counter to the console
    private static void PrintWarningCounter<T>(ColCounter<T> project_counter, string project, Func<T, string> display)
        where T : notnull
    {
        AnsiConsole.WriteLine($"{project_counter.TotalCount()} warnings in {project}.");
        foreach (var (file, count) in project_counter.MostCommon().Take(10))
        {
            AnsiConsole.WriteLine($"{display(file)} at {count}");
        }
    }

    public void WriteFinalReport(GlobalStatistics stats)
    {
        if (!args.ShortArgs && args.Only.Length == 0)
        {
            foreach (var (project, warnings) in stats.ProjectCounters)
            {
                PrintWarningCounter(warnings, project, f => f.GetDisplay());
                AnsiConsole.WriteLine("");
                AnsiConsole.WriteLine("");
            }
        }
        if (false == args.ShortArgs && args.Only.Length == 0)
        {
            PrintReportToConsole(stats);
        }
    }

    public void SingleFileReport(Fil source_file, SingleFileReport report)
    {
        var short_list = args.ShortArgs;
        var only_show_these_classes = args.Only;
        var stats = report.Stats;

        if (false == short_list && only_show_these_classes.Length == 0)
        {
            AnsiConsole.WriteLine($"took {report.TimeTaken}");
        }

        bool first = true;
        foreach (var g in report.GroupedMessages)
        {
            if(first) first = false;
            else
            {
                for(int i=0; i<4; i+=1)
                {
                    Console.WriteLine();
                }
            }
            if (only_show_these_classes.Length > 0)
            {
                var all_warning_classes = g.messages.First().GetClasses();
                var show_this_warning = all_warning_classes.Any(warning_class => only_show_these_classes.Contains(warning_class));
                if (!show_this_warning) continue;
            }

            bool first_message = true;
            foreach (var m in g.messages)
            {
                if(first_message) first_message = false;
                else Console.WriteLine();

                var cs = m.Category != null ? $"[{m.Category}]" : string.Empty;
                Console.WriteLine($"{m.File.GetDisplay()} ({m.Line}/{m.Column}) {m.Type}: {m.Message}{cs}");
                foreach (var line in m.Code)
                {
                    Console.WriteLine(line);
                }
            }
        }

        if (false == short_list && only_show_these_classes.Length == 0)
        {
            PrintWarningCounter(stats.Classes, source_file.GetDisplay(), c => c);
            AnsiConsole.WriteLine("");
        }
    }

    private static void PrintReportToConsole(GlobalStatistics stats)
    {
        Printer.Header("TIDY REPORT");
        PrintWarningCounter(stats.TotalCounter, "total", f => f.GetDisplay());
        AnsiConsole.WriteLine("");
        PrintWarningCounter(stats.TotalClasses, "classes", c => c);
        AnsiConsole.WriteLine("");
        Printer.Line();
        AnsiConsole.WriteLine("");
        foreach (var (k, v) in stats.WarningsPerFile)
        {
            AnsiConsole.WriteLine($"{k}:");
            foreach (var f in v)
            {
                AnsiConsole.WriteLine($"  {f.GetDisplay()}");
            }
            AnsiConsole.WriteLine("");
        }

        Printer.Line();
        AnsiConsole.WriteLine("");

        var tt = stats.GetTimeTaken();
        if(tt != null)
        {
            AnsiConsole.MarkupLineInterpolated($"average: {tt.average_value}");
            AnsiConsole.MarkupLineInterpolated($"max: {tt.ma.Value}s for {tt.ma.Key.GetDisplay()}");
            AnsiConsole.MarkupLineInterpolated($"min: {tt.mi.Value} for {tt.mi.Key.GetDisplay()}");
            AnsiConsole.MarkupLineInterpolated($"{tt.times_per_file_count} files");
        }
    }
}

internal class HtmlOutput(Dir args_html_root) : IOutput
{
    private readonly string root_name = "Tidy report";
    private readonly Dir root_relative = Dir.CurrentDirectory;
    private readonly Dir root_output = args_html_root;
    private readonly List<HtmlLink> root_links = new();
    private readonly Dictionary<Fil, HtmlLink> source_file_to_link = new();
    private GlobalStatistics? global_stats = null;

    private sealed class HtmlLink
    {
        public HtmlLink(string ti, string li, TimeSpan ta, int ca, int to)
        {
            Title = ti;
            Link = li;
            TimeTaken = ta;
            Categories = ca;
            Totals = to;
        }

        public string Title { get; }
        public string Link { get; }
        public TimeSpan TimeTaken { get; }
        public int Categories { get; }
        public int Totals { get; }
    }

    private string LinkToFile(Fil source_file)
    {
        if(source_file_to_link.TryGetValue(source_file, out var link))
        {
            return $"<a href=\"{link.Link}\">{link.Title}</a>";
        }
        else
        {
            return source_file.GetDisplay();
        }
    }

    public void WriteIndexFile()
    {
        List<string> output = new();

        output.Add($"<html>");
        output.Add($"<head>");
        output.Add("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        output.Add("<link rel=\"stylesheet\" href=\"https://cdn.jsdelivr.net/npm/water.css@2/out/water.css\">");
        output.Add($"<title>{root_name}</title>");
        output.Add($"</head>");
        output.Add($"<body>");

        output.Add($"<h1>{root_name}</h1>");

        if(global_stats != null)
        {
            var tt = global_stats.GetTimeTaken();
            if(tt != null)
            {
                output.Add($"<p><b>average</b>: {tt.average_value}</p>");
                output.Add($"<p><b>max</b>: {tt.ma.Value}s for {LinkToFile(tt.ma.Key)}</p>");
                output.Add($"<p><b>min</b>: {tt.mi.Value} for {LinkToFile(tt.mi.Key)}</p>");
                output.Add($"<p>{tt.times_per_file_count} files</p>");
            }

            var project_counter = global_stats.TotalClasses;
            output.Add($"<h2>{project_counter.TotalCount()} warnings</h2>");
            output.Add("<ul>");
            foreach (var (file, count) in project_counter.MostCommon().Take(10))
            {
                output.Add($"<li>{file} at {count}</li>");
            }
            output.Add("</ul>");

            // todo(Gustav): merge these 2 lists

            foreach (var (k, v) in global_stats.WarningsPerFile)
            {
                output.Add($"<h3>{k}</h3>");
                output.Add("<ul>");
                foreach (var f in v)
                {
                    output.Add($"<li>{LinkToFile(f)}</li>");
                }
                output.Add("</ul>");
                AnsiConsole.WriteLine("");
            }
        }

        var align = " style=\"text-align: end\"";

        output.Add("<table style=\"width: 100%\">");
        output.Add("<colgroup>");
        output.Add("<col span=\"1\" style=\"width: 70%;\">");
        output.Add("<col span=\"1\" style=\"width: 15%;\">");
        output.Add("<col span=\"1\" style=\"width: 15%;\">");
        output.Add("</colgroup>");
        output.Add("<thead>");
        output.Add($"<tr>");
        output.Add("<th>File</th>");
        output.Add($"<th{align}>Categories</th>");
        output.Add($"<th{align}>Totals</th>");
        output.Add($"</tr>");
        output.Add("</thead>");
        output.Add("<tbody>");
        foreach (var l in root_links.OrderByDescending(l => l.Totals))
        {
            output.Add($"<tr>");
            output.Add($"<td><a href={l.Link}>{l.Title}</a></td>");
            output.Add($"<td{align}>{Q(l.Categories)}</td>");
            output.Add($"<td{align}>{Q(l.Totals)}</td>");
            output.Add($"</tr>");
        }
        output.Add("</tbody>");
        output.Add($"</table>");

        output.Add($"</body>");
        output.Add($"</html>");

        var target = this.root_output.GetFile("index.html");

        target.Directory?.CreateDir();
        target.WriteAllLines(output);
        Console.WriteLine($"Wrote html to {target}");

        static string Q(int i)
        {
            if (i >= 0) return $"{i}";
            else return "?";
        }
    }

    public string GetRelative(Fil f)
        => this.root_relative.RelativeFromTo(f);

    public Fil GetOutput(Fil f, string ext)
        => this.root_output.GetFile(GetRelative(f)).ChangeExtension(ext);

    public void WriteFinalReport(GlobalStatistics stats)
    {
        global_stats = stats;
        WriteIndexFile();
    }

    private string TryRelative(string path)
    {
        var f = new Fil(path);
        var suggested = GetRelative(f);

        // if returned path includes back references, just use full path?
        if (suggested.StartsWith(".")) return path;

        return suggested;
    }

    public void SingleFileReport(Fil source_file, SingleFileReport report)
    {
        var name = GetRelative(source_file);
        var target = GetOutput(source_file, ".html");

        var grouped = report.GroupedMessages;

        // todo(Gustav): expand with more data from the report

        List<string> output = new();

        output.Add($"<html>");
        output.Add($"<head>");
        output.Add("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        output.Add("<link rel=\"stylesheet\" href=\"https://cdn.jsdelivr.net/npm/water.css@2/out/water.css\">");
        output.Add($"<title>{name}</title>");
        output.Add($"</head>");
        output.Add($"<body>");

        output.Add($"<h1>{name}</h1>");

        var count = new HashSet<string>();

        foreach (var g in grouped)
        {
            foreach (var m in g.messages)
            {
                var is_note = m.Type == "note";

                if (is_note == false)
                {
                    output.Add("<hr>");
                    output.Add($"<h3>{m.Type}: {m.Message}</h3>");
                }

                if (m.Category != null)
                {
                    output.Add($"<code>[{m.Category}]</code>");
                    count.Add(m.Category);
                }

                if (is_note)
                {
                    output.Add($"<p>{m.Message}</p>");
                }

                output.Add($"<p><i>{LinkToFile(m.File)} {m.Line} : {m.Column}</i></p>");

                output.Add($"<pre>");
                foreach (var l in m.Code)
                {
                    output.Add(l);
                }
                output.Add($"</pre>");
            }
        }

        // print original report for debugging...
        output.Add($"<!--");
        foreach(var line in report.Messages)
        {
            output.Add(line);
        }
        output.Add($"-->");

        output.Add($"</body>");
        output.Add($"</html>");

        target.Directory?.CreateDir();
        target.WriteAllLines(output);
        Console.WriteLine($"Wrote html to {target}");

        AddFile(source_file, name, target, report.TimeTaken, grouped.Length, count.Count);
        WriteIndexFile();
    }

    private void AddFile(Fil source_file, string name, Fil target, TimeSpan time_taken, int totals, int categories)
    {
        var link = new HtmlLink(name, this.root_output.RelativeFromTo(target), time_taken, categories, totals);
        root_links.Add(link);
        source_file_to_link.Add(source_file, link);
    }
}

internal static class ClangTidy
{
    private static Fil GetPathToStore(Dir build_folder)
        => build_folder.GetFile(FileNames.ClangTidyStore);

    private static Store? LoadStore(Log print, Dir build_folder)
    {
        var file_name = GetPathToStore(build_folder);
        AnsiConsole.MarkupLineInterpolated($"Loading store from {file_name}");
        if (!file_name.Exists)
        {
            Console.WriteLine("Failed to load");
            return new Store();
        }

        var content = file_name.ReadAllText();
        if (string.IsNullOrWhiteSpace(content))
        {
            return new Store();
        }
        var loaded = JsonUtil.Parse<JsonStore>(print, file_name, content);

        return loaded != null ? new(loaded) : null;
    }

    private static void SaveStore(Dir build_folder, Store data)
    {
        var file_name = GetPathToStore(build_folder);
        file_name.WriteAllText(JsonUtil.Write(new JsonStore(data)));
    }

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
        var root_file = ClangTidyFile.GetPathToClangTidySource(root);

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
        var clang_tidy_source = ClangTidyFile.GetPathToClangTidySource(root);

        var data = new StoredTidyUpdate(output.Output, output.Taken,
            GetLastModificationForFiles(new[] { clang_tidy_source, source_file }));
        store.Cache[source_file] = data;
        SaveStore(project_build_folder, store);
        Console.WriteLine($"Stored in cache {store.Cache.Count}");
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
        }

        var end = DateTime.Now;
        var took = end - start;
        var ret = new TidyOutput(output.Output.Select(x => x.Line).ToArray(), took);
        return ret;
    }

    private static FileStats CreateStatistics(Fil source_file, TidyOutput tidy_output)
    {
        var stats = new FileStats();

        // todo(Gustav): use parsed group here instead of parsing again?
        foreach (var line in RemoveStatusLines(tidy_output.Output))
        {
            if (line.Contains("warning: "))
            {
                stats.Warnings.AddOne(source_file);
                var tidy_class = ClangTIdyParsing.ClangTidyWarningClass().Match(line);
                if (tidy_class.Success)
                {
                    var warning_classes = tidy_class.Groups[1];
                    foreach (var warning_class in warning_classes.Value.Split(','))
                    {
                        stats.Classes.AddOne(warning_class);
                    }
                }
            }
        }

        return stats;
    }

    private static IEnumerable<string> RemoveStatusLines(string[] output)
    {
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
                yield return line;
            }
        }
    }

    public class Args(Dir? html_root, int task_count, bool fix, string[] filter, bool nop, bool short_args, bool force, string[] only)
    {
        public Dir? HtmlRoot { get; } = html_root;
        public int NumberOfTasks { get; } = task_count;
        public bool Fix { get; } = fix;
        public bool Force { get; } = force;
        public bool ShortArgs { get; } = short_args;
        public bool Nop { get; } = nop;
        public string[] Filter { get; } = filter;
        public string[] Only { get; } = only;
    }

    // callback function called when running clang.py tidy
    internal static async Task<int> HandleRunClangTidyCommand(CompileCommandsArguments cc, Log log, bool also_include_headers, Args args)
    {
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

        ClangTidyFile.WriteTidyFileToDisk(root);
        AnsiConsole.WriteLine($"using clang-tidy: {clang_tidy}");

        IOutput output = args.HtmlRoot == null ? new ConsoleOutput(args) : new HtmlOutput(args.HtmlRoot);

        var files = ClangFiles.MapAllFilesInRootOnFirstDir(root, also_include_headers ? FileUtil.IsHeaderOrSource : FileUtil.IsSource, FileSection.AllExceptThoseIgnoredByClangTidy);
        var stats = await RunAllFiles(log, args, files, store, root, clang_tidy, project_build_folder, output);

        output.WriteFinalReport(stats);

        if (stats.TotalCounter.TotalCount() > 0)
        {
            return -1;
        }
        else
        {
            return 0;
        }
    }

    private sealed class CollectedTidyFil
    {
        public Fil File { get; }
        public string Category { get; }

        public CollectedTidyFil(Fil file, string category)
        {
            File = file;
            Category = category;
        }
    }

    private static async Task<GlobalStatistics> RunAllFiles(Log log, Args args, CategoryAndFiles[] data, Store store, Dir root, Fil clang_tidy,
        Dir project_build_folder, IOutput html_root)
    {
        var files = data.SelectMany(pair => pair.Files.Select(x => new CollectedTidyFil(x, pair.Category)))
            .Where(source_file => FileMatchesAllFilters(source_file.File, args.Filter) == false);

        var stats = new GlobalStatistics();

        await Channel
            .CreateUnbounded<CollectedTidyFil>()
            .Source(files)
            .PipeAsync(
                maxConcurrency: args.NumberOfTasks,
                capacity: 100,
                transform: async source_file =>
                {
                    AnsiConsole.WriteLine($"Running {source_file.File}");
                    var tidy_output = args.Nop
                        ? new([], new())
                        : await GetExistingOutputOrCallClangTidy(store, log, root, args.Force, clang_tidy, project_build_folder, source_file.File, args.Fix);
                    return (source_file.Category, source_file.File, tidy_output);
                })
            .ReadAll(tuple =>
            {
                var (cat, source_file, tidy_output) = tuple;
                AnsiConsole.WriteLine($"Collecting {source_file}");

                stats.AddTimeTaken(source_file, tidy_output.Taken);
                var file_stats = CreateStatistics(source_file, tidy_output);

                var report = new SingleFileReport(tidy_output.Taken, RemoveStatusLines(tidy_output.Output), file_stats);

                html_root.SingleFileReport(source_file, report);

                // if (file_stats.warnings.Items.Any())
                // {
                //     stats.Errors.Add(new FileWithError(source_file, file_stats.lines.ToArray()));
                // }

                stats.GetProjectCounter(cat).Update(file_stats.Warnings);
                stats.TotalCounter.Update(file_stats.Warnings);
                stats.TotalClasses.Update(file_stats.Classes);
                foreach (var k in file_stats.Classes.Keys)
                {
                    stats.GetWarningsList(k).Add(source_file);
                }
            });

        return stats;
    }
}

internal static class ClangFormat
{
    // callback function called when running clang.py format
    internal static async Task<int> HandleClangFormatCommand(Log log, bool nop)
    {
        var clang_format_path = Config.Paths.GetClangFormatExecutable(log);
        if (clang_format_path == null)
        {
            return -1;
        }

        var root = Dir.CurrentDirectory;

        var data = ClangFiles.MapAllFilesInRootOnFirstDir(root, FileUtil.IsHeaderOrSource, FileSection.AllFiles);

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
