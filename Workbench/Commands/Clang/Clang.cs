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

using Workbench.Shared.Extensions;

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

internal record TimeTaken(TimeSpan AverageValue, KeyValuePair<Fil, TimeSpan> Min, KeyValuePair<Fil, TimeSpan> Max, int TimesPerFileCount);

internal class GlobalStatistics
{
    private readonly Dictionary<Fil, TimeSpan> times_per_file = new();

    public Dictionary<string, ColCounter<Fil>> ProjectCounters { get; } = new();

    public Dictionary<string, List<Fil>> WarningsPerFile { get; } = new();

    public ColCounter<Fil> TotalCounter { get; } = new();
    public ColCounter<string> TotalClasses { get; } = new();

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
    // todo(Gustav) parse out to a enum
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Category { get; set; } = null;
    public List<string> Code { get; set; } = new();

    public static IEnumerable<TidyMessage> Parse(Log print, IEnumerable<string> lines)
    {
        var reg = new Regex(@"(?<file>([a-zA-Z]:)?[^:]+):(?<line>[0-9]+):(?<col>[0-9]+): (?<type>[^$]+):(?<mess>[^$]+)");
        TidyMessage? current = null;
        foreach (var l in lines)
        {
            if (string.IsNullOrEmpty(l)) continue;

            var is_code = l.TrimStart() != l; // does the line start with space?
            if (is_code)
            {
                if (current == null)
                {
                    print.Warning($"Unexpected code line: '{l}'");
                    continue;
                }
                current.Code.Add(l);
            }
            else
            {
                if (current != null) yield return current;

                var l2 = l;

                var tidy_class = ClangTidyParsing.ClangTidyWarningClass().Match(l2);
                if (tidy_class.Success)
                {
                    l2 = l2.Substring(0, tidy_class.Index).Trim();
                }

                var parsed = reg.Match(l2);
                if (parsed.Success == false)
                {
                    print.Warning($"Invalid line: '{l}'");
                    current = null;
                    continue;
                }
                string? cat = null;
                if (tidy_class.Success) cat = tidy_class.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(cat)) cat = null;
                var parsed_file = Fil.CleanupRelative(parsed.Groups["file"].Value.Trim());
                current = new TidyMessage(new Fil(parsed_file))
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
    // todo(Gustav): replace message type with something more appropriate
    public List<TidyMessage> Messages { get; set; } = new();

    public static IEnumerable<TidyGroup> Parse(Log print, IEnumerable<string> lines)
    {
        TidyGroup? current = null;
        foreach (var mess in TidyMessage.Parse(print, lines))
        {
            if (current == null || mess.Type != "note")
            {
                if (current != null) yield return current;
                current = new TidyGroup();
            }
            current.Messages.Add(mess);
        }

        if (current != null)
        {
            yield return current;
        }
    }
}


public static class ClangTidyFile
{
    internal static Fil GetPathToClangTidySource(Dir root)
        => root.GetFile("clang-tidy");

    // return a iterator over the "compiled" .clang-tidy lines
    private static IEnumerable<string> GenerateClangTidyAsIterator(Vfs vfs, Dir root)
    {
        var clang_tidy_file = GetPathToClangTidySource(root).ReadAllLines(vfs);
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
    private static void PrintGeneratedClangTidy(Log print, Vfs vfs, Dir root)
    {
        foreach (var line in GenerateClangTidyAsIterator(vfs, root))
        {
            print.Info(line);
        }
    }

    // write the .clang-tidy from the clang-tidy "source"
    public static void WriteTidyFileToDisk(Vfs vfs, Dir root)
    {
        var content = GenerateClangTidyAsIterator(vfs, root);
        root.GetFile(".clang-tidy").WriteAllLines(vfs, content);
    }

    internal static void HandleMakeTidyCommand(Log print, Vfs vfs, Dir cwd, bool nop)
    {
        if (nop)
        {
            PrintGeneratedClangTidy(print, vfs, cwd);
        }
        else
        {
            WriteTidyFileToDisk(vfs, cwd);
        }
    }
}

public enum FileSection
{
    AllFiles, AllExceptThoseIgnoredByClangTidy
}

internal static class ClangFiles
{
    private static bool IsFileIgnoredByClangTidy(Vfs vfs, Fil path)
    {
        var first_line = path.ReadAllLines(vfs).FirstOrDefault();
        if (first_line == null) { return false; }

        return first_line.StartsWith("// clang-tidy: ignore");
    }

    private static CategoryAndFiles[] MapFilesOnFirstDir(Vfs vfs, Dir root, FileSection fs, IEnumerable<Fil> files_iterator)
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
                        .Where(f => fs == FileSection.AllFiles || !IsFileIgnoredByClangTidy(vfs, f))
                        .OrderBy(p => p.Name)
                        .ThenByDescending(p => p.Extension)
                        .ToArray()
                )
            ).ToArray();

    internal static CategoryAndFiles[] MapAllFilesInRootOnFirstDir(Vfs vfs, Dir root, Func<Fil, bool> extension_filter, FileSection fs)
        => MapFilesOnFirstDir(vfs, root, fs, FileUtil.FilesInPitchfork(vfs, root, false)
            .Where(extension_filter));

    internal static int HandleTidyListFilesCommand(Vfs vfs, Dir cwd, Log print, bool sort_files, FileSection fs)
    {
        var files = FileUtil.IterateFiles(vfs, cwd, false, true)
            .Where(FileUtil.IsSource);

        if (sort_files)
        {
            var sorted = MapFilesOnFirstDir(vfs, cwd, fs, files);
            foreach (var (project, source_files) in sorted)
            {
                Printer.Header(project);
                foreach (var file in source_files)
                {
                    print.Info(file.GetDisplay(cwd));
                }
                print.Info("");
            }
        }
        else
        {
            foreach (var file in files)
            {
                print.Info(file.GetDisplay(cwd));
            }
        }

        return 0;
    }
}

internal static partial class ClangTidyParsing
{
    [GeneratedRegex(@"\[(\w+([-,]\w+)+)\]", RegexOptions.Compiled)]
    public static partial Regex ClangTidyWarningClass();
}

internal class SingleFileReport
{
    public SingleFileReport(Log print, TimeSpan taken, IEnumerable<string> lines, FileStats file_stats)
    {
        Messages = [.. lines];
        GroupedMessages = [.. TidyGroup.Parse(print, Messages)];
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
    void WriteFinalReport(Vfs vfs, Dir cwd, GlobalStatistics stats);
    void SingleFileReport(Vfs vfs, Dir cwd, Fil source_file, SingleFileReport report);
}

internal class ConsoleOutput(Args args, Log print) : IOutput
{
    // print warning counter to the console
    private static void PrintWarningCounter<T>(Log print, ColCounter<T> project_counter, string project, Func<T, string> display)
        where T : notnull
    {
        print.Info($"{project_counter.TotalCount()} warnings in {project}.");
        foreach (var (file, count) in project_counter.MostCommon().Take(10))
        {
            print.Info($"{display(file)} at {count}");
        }
    }

    public void WriteFinalReport(Vfs _, Dir cwd, GlobalStatistics stats)
    {
        if (!args.ShortArgs && args.Only.Length == 0)
        {
            foreach (var (project, warnings) in stats.ProjectCounters)
            {
                PrintWarningCounter(print, warnings, project, f => f.GetDisplay(cwd));
                print.Info("");
                print.Info("");
            }
        }
        if (false == args.ShortArgs && args.Only.Length == 0)
        {
            PrintReportToConsole(print, cwd, stats);
        }
    }

    public void SingleFileReport(Vfs _, Dir cwd, Fil source_file, SingleFileReport report)
    {
        var short_list = args.ShortArgs;
        var only_show_these_classes = args.Only;
        var stats = report.Stats;

        if (false == short_list && only_show_these_classes.Length == 0)
        {
            print.Info($"took {report.TimeTaken}");
        }

        bool first = true;
        foreach (var g in report.GroupedMessages)
        {
            if(first) first = false;
            else
            {
                for(int i=0; i<4; i+=1)
                {
                    print.Info("");
                }
            }
            if (only_show_these_classes.Length > 0)
            {
                var all_warning_classes = g.Messages.First().GetClasses();
                var show_this_warning = all_warning_classes.Any(warning_class => only_show_these_classes.Contains(warning_class));
                if (!show_this_warning) continue;
            }

            bool first_message = true;
            foreach (var m in g.Messages)
            {
                if(first_message) first_message = false;
                else print.Info("");

                var cs = m.Category != null ? $"[{m.Category}]" : string.Empty;
                print.Raw($"{m.File.GetDisplay(cwd)} ({m.Line}/{m.Column}) {m.Type}: {m.Message}{cs}");
                foreach (var line in m.Code)
                {
                    print.Raw(line);
                }
            }
        }

        if (false == short_list && only_show_these_classes.Length == 0)
        {
            PrintWarningCounter(print, stats.Classes, source_file.GetDisplay(cwd), c => c);
            print.Info("");
        }
    }

    private static void PrintReportToConsole(Log print, Dir cwd, GlobalStatistics stats)
    {
        Printer.Header("TIDY REPORT");
        PrintWarningCounter(print, stats.TotalCounter, "total", f => f.GetDisplay(cwd));
        print.Info("");
        PrintWarningCounter(print, stats.TotalClasses, "classes", c => c);
        print.Info("");
        Printer.Line();
        print.Info("");
        foreach (var (k, v) in stats.WarningsPerFile)
        {
            print.Info($"{k}:");
            foreach (var f in v)
            {
                print.Info($"  {f.GetDisplay(cwd)}");
            }
            print.Info("");
        }

        Printer.Line();
        print.Info("");

        var tt = stats.GetTimeTaken();
        if(tt != null)
        {
            print.Info($"average: {tt.AverageValue.ToHumanString()}");
            print.Info($"max: {tt.Max.Value.ToHumanString()} for {tt.Max.Key.GetDisplay(cwd)}");
            print.Info($"min: {tt.Min.Value.ToHumanString()} for {tt.Min.Key.GetDisplay(cwd)}");
            print.Info($"{tt.TimesPerFileCount} files");
        }
    }
}

internal class HtmlOutput(Log print, Dir root_output, Dir dcwd) : IOutput
{
    private readonly string root_name = "Tidy report";
    private readonly Dir root_relative = dcwd;
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

    private string LinkToFile(Dir cwd, Fil source_file)
        => source_file_to_link.TryGetValue(source_file, out var link)
            ? $"<a href=\"{link.Link}\">{link.Title}</a>"
            : source_file.GetDisplay(cwd)
            ;

    public void WriteIndexFile(Vfs vfs, Dir cwd)
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

        var tt = global_stats?.GetTimeTaken();

        if(global_stats != null && tt != null)
        {
            output.Add("<h2>Summary</h2>");
            var project_counter = global_stats.TotalClasses;
            output.Add($"<p>{project_counter.TotalCount()} warnings in {tt.TimesPerFileCount} files</p>");
            output.Add("<ul>");
            foreach (var (klass, count) in project_counter.MostCommon())
            {
                output.Add($"<li>{count}x <code>{klass}</code></li>");

                if(global_stats.WarningsPerFile.TryGetValue(klass, out var v))
                {
                    output.Add("<ul>");
                    foreach (var f in v)
                    {
                        output.Add($"<li>{LinkToFile(cwd, f)}</li>");
                    }
                    output.Add("</ul>");
                }
            }
            output.Add("</ul>");

            output.Add("<h2>All files</h2>");
        }

        var align = " style=\"text-align: end\"";
        output.Add("<table style=\"width: 100%\">");
        output.Add("<colgroup>");
        output.Add("<col span=\"1\" style=\"width: 55%;\">");
        output.Add("<col span=\"1\" style=\"width: 15%;\">");
        output.Add("<col span=\"1\" style=\"width: 15%;\">");
        output.Add("<col span=\"1\" style=\"width: 15%;\">");
        output.Add("</colgroup>");
        output.Add("<thead>");
        output.Add($"<tr>");
        output.Add("<th>File</th>");
        output.Add($"<th{align}>Categories</th>");
        output.Add($"<th{align}>Totals</th>");
        output.Add($"<th{align}>Time</th>");
        output.Add($"</tr>");
        output.Add("</thead>");
        output.Add("<tbody>");
        foreach (var l in root_links.OrderByDescending(l => l.Totals))
        {
            output.Add($"<tr>");
            output.Add($"<td><a href={l.Link}>{l.Title}</a></td>");
            output.Add($"<td{align}>{Q(l.Categories)}</td>");
            output.Add($"<td{align}>{Q(l.Totals)}</td>");
            output.Add($"<td{align}>{l.TimeTaken.ToHumanString()}</td>");
            output.Add($"</tr>");
        }
        output.Add("</tbody>");
        output.Add("</table>");

        if(tt != null)
        {
            output.Add("<h3>Timings</h3>");
            output.Add($"<p><b>average</b>: {tt.AverageValue.ToHumanString()}</p>");
            output.Add($"<p><b>max</b>: {tt.Max.Value.ToHumanString()} for {LinkToFile(cwd, tt.Max.Key)}</p>");
            output.Add($"<p><b>min</b>: {tt.Min.Value.ToHumanString()} for {LinkToFile(cwd, tt.Min.Key)}</p>");
        }

        output.Add($"</body>");
        output.Add($"</html>");

        var target = root_output.GetFile("index.html");

        target.Directory?.CreateDir(vfs);
        target.WriteAllLines(vfs, output);
        print.Info($"Wrote html to {target}");
        return;

        static string Q(int i)
            => i >= 0
                ? $"{i}"
                : "?"
        ;
    }

    public string GetRelative(Fil f)
        => this.root_relative.RelativeFromTo(f);

    public Fil GetOutput(Fil f, string ext)
        => root_output.GetFile(GetRelative(f)).ChangeExtension(ext);

    public void WriteFinalReport(Vfs vfs, Dir cwd, GlobalStatistics stats)
    {
        global_stats = stats;
        WriteIndexFile(vfs, cwd);
    }

    public void SingleFileReport(Vfs vfs, Dir cwd, Fil source_file, SingleFileReport report)
    {
        var name = GetRelative(source_file);
        var target = GetOutput(source_file, ".html");

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

        foreach (var g in report.GroupedMessages)
        {
            foreach (var m in g.Messages)
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

                output.Add($"<p><i>{LinkToFile(cwd, m.File)} {m.Line} : {m.Column}</i></p>");

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

        target.Directory?.CreateDir(vfs);
        target.WriteAllLines(vfs, output);
        print.Info($"Wrote html to {target}");

        AddFile(source_file, name, target, report.TimeTaken, report.GroupedMessages.Length, count.Count);
        WriteIndexFile(vfs, cwd);
    }

    private void AddFile(Fil source_file, string name, Fil target, TimeSpan time_taken, int totals, int categories)
    {
        var link = new HtmlLink(name, root_output.RelativeFromTo(target), time_taken, categories, totals);
        root_links.Add(link);
        source_file_to_link.Add(source_file, link);
    }
}

public class ClangTidy
{
    private static Fil GetPathToStore(Dir build_folder)
        => build_folder.GetFile(FileNames.ClangTidyStore);

    private static Store? LoadStore(Vfs vfs, Log print, Dir build_folder)
    {
        var file_name = GetPathToStore(build_folder);
        AnsiConsole.MarkupLineInterpolated($"Loading store from {file_name}");
        if (!file_name.Exists(vfs))
        {
            print.Warning($"Failed to load store from {file_name}");
            return new Store();
        }

        var content = file_name.ReadAllText(vfs);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new Store();
        }
        var loaded = JsonUtil.Parse<JsonStore>(print, file_name, content);

        return loaded != null ? new(loaded) : null;
    }

    private static void SaveStore(Vfs vfs, Dir build_folder, Store data)
    {
        var file_name = GetPathToStore(build_folder);
        file_name.WriteAllText(vfs, JsonUtil.Write(new JsonStore(data)));
    }

    private static bool FileMatchesAllFilters(Fil file, string[]? filters)
        => filters != null && filters.All(f => file.Path.Contains(f) == false);

    private static DateTime GetLastModification(Vfs vfs, Fil file)
        => file.LastWriteTimeUtc(vfs);

    private static DateTime GetLastModificationForFiles(Vfs vfs, IEnumerable<Fil> input_files)
     => input_files.Select(f => GetLastModification(vfs, f)).Max();

    private static bool IsModificationTheLatest(Vfs vfs, IEnumerable<Fil> input_files, DateTime output)
        => GetLastModificationForFiles(vfs, input_files) <= output;

    private static TidyOutput? GetExistingOutputOrNull(Vfs vfs, Store store, Dir root, Fil source_file)
    {
        var root_file = ClangTidyFile.GetPathToClangTidySource(root);

        if (store.Cache.TryGetValue(source_file, out var stored) == false)
        {
            return null;
        }

        if (IsModificationTheLatest(vfs, new[] { root_file, source_file }, stored.Modified))
        {
            return new TidyOutput(stored.Output, stored.Taken);
        }

        return null;
    }

    private static void StoreOutput(Log print, Vfs vfs, Store store, Dir root, Dir project_build_folder,
        Fil source_file, TidyOutput output)
    {
        var clang_tidy_source = ClangTidyFile.GetPathToClangTidySource(root);

        var data = new StoredTidyUpdate(output.Output, output.Taken,
            GetLastModificationForFiles(vfs, new[] { clang_tidy_source, source_file }));
        store.Cache[source_file] = data;
        SaveStore(vfs, project_build_folder, store);
        print.Info($"Stored in cache {store.Cache.Count}");
    }

    // runs clang-tidy and returns all the text output
    private static async Task<TidyOutput> GetExistingOutputOrCallClangTidy(Executor exec, Vfs vfs, Dir cwd, Store store,
        Log print, Dir root, bool force, Fil tidy_path, Dir project_build_folder,
        Fil source_file, bool fix)
    {
        if (false == force)
        {
            var existing_output = GetExistingOutputOrNull(vfs, store, root, source_file);
            if (existing_output != null)
            {
                return existing_output;
            }
        }

        var ret = await CallClangTidyAsync(exec, cwd, print, tidy_path, project_build_folder, source_file, fix);
        StoreOutput(print, vfs, store, root, project_build_folder, source_file, ret);
        return ret;
    }

    private static async Task<TidyOutput> CallClangTidyAsync(Executor exec, Dir cwd, Log log, Fil tidy_path,
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
        var output = await command.RunAndGetOutputAsync(exec, cwd);

        if (output.ExitCode != 0)
        {
            log.Error($"{tidy_path} exited with {output.ExitCode}");
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
                var tidy_class = ClangTidyParsing.ClangTidyWarningClass().Match(line);
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

    private static IEnumerable<string> RemoveStatusLines(IEnumerable<string> output)
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
    public async Task<int> HandleRunClangTidyCommand(Executor exec, Vfs vfs, Config.Paths paths, Dir cwd, CompileCommandsArguments cc, Log print, bool also_include_headers, Args args)
    {
        var clang_tidy = paths.GetClangTidyExecutable(vfs, cwd, print);
        if (clang_tidy == null)
        {
            return -1;
        }

        var cc_file = CompileCommand.FindOrNone(vfs, cwd, cc, print, paths);
        if (cc_file == null)
        {
            return -1;
        }

        var project_build_folder = cc_file.Directory;
        if (project_build_folder is null)
        {
            print.Error("unable to find build folder");
            return -1;
        }

        var store = LoadStore(vfs, print, project_build_folder);
        if (store == null)
        {
            print.Error("unable to find load store");
            return -1;
        }

        ClangTidyFile.WriteTidyFileToDisk(vfs, cwd);
        print.Info($"using clang-tidy: {clang_tidy}");

        IOutput output = args.HtmlRoot == null ? new ConsoleOutput(args, print) : new HtmlOutput(print, args.HtmlRoot, cwd);

        var files = ClangFiles.MapAllFilesInRootOnFirstDir(vfs, cwd, also_include_headers ? FileUtil.IsHeaderOrSource : FileUtil.IsSource, FileSection.AllExceptThoseIgnoredByClangTidy);
        var stats = await RunAllFiles(exec, vfs, cwd, print, args, files, store, cwd, clang_tidy, project_build_folder, output);

        output.WriteFinalReport(vfs, cwd, stats);

        if (stats.TotalCounter.TotalCount() > 0)
        {
            return -1;
        }
        else
        {
            return 0;
        }
    }

    private sealed class CollectedTidyFil(Fil file, string category)
    {
        public Fil File { get; } = file;
        public string Category { get; } = category;
    }

    private record TransformRec(string Category, Fil File, TidyOutput tidy_output);

    private static async Task<GlobalStatistics> RunAllFiles(Executor exec, Vfs vfs, Dir cwd, Log print, Args args, CategoryAndFiles[] data, Store store, Dir root, Fil clang_tidy,
        Dir project_build_folder, IOutput html_root)
    {
        var files = data.SelectMany(pair => pair.Files.Select(x => new CollectedTidyFil(x, pair.Category)))
            .Where(source_file => FileMatchesAllFilters(source_file.File, args.Filter) == false);

        var stats = new GlobalStatistics();

        // todo(Gustav): remove if statement and only have one way through the loop...
        if (args.NumberOfTasks == 1)
        {
            foreach (var f in files)
            {
                var tradat = await transform_function(exec, f);
                read_function(tradat);
            }
        }
        else
        {
            await Channel
                .CreateUnbounded<CollectedTidyFil>()
                .Source(files)
                .PipeAsync(
                    maxConcurrency: args.NumberOfTasks,
                    capacity: 100,
                    transform: async source_file => await transform_function(exec, source_file))
                .ReadAll(read_function);
        }

        return stats;

        async Task<TransformRec> transform_function(Executor exec, CollectedTidyFil source_file)
        {
            print.Info($"Running {source_file.File}");
            var tidy_output = args.Nop
                ? new([], new())
                : await GetExistingOutputOrCallClangTidy(exec, vfs, cwd, store, print, root, args.Force, clang_tidy, project_build_folder, source_file.File, args.Fix);
            return new(source_file.Category, source_file.File, tidy_output);
        }

        void read_function(TransformRec tuple)
        {
            var (cat, source_file, tidy_output) = tuple;
            print.Info($"Collecting {source_file}");

            stats.AddTimeTaken(source_file, tidy_output.Taken);
            var file_stats = CreateStatistics(source_file, tidy_output);

            var report = new SingleFileReport(print, tidy_output.Taken, RemoveStatusLines(tidy_output.Output), file_stats);

            html_root.SingleFileReport(vfs, cwd, source_file, report);

            stats.GetProjectCounter(cat).Update(file_stats.Warnings);
            stats.TotalCounter.Update(file_stats.Warnings);
            stats.TotalClasses.Update(file_stats.Classes);
            foreach (var k in file_stats.Classes.Keys)
            {
                stats.GetWarningsList(k).Add(source_file);
            }
        }
    }
}

internal static class ClangFormat
{
    // callback function called when running clang.py format
    internal static async Task<int> HandleClangFormatCommand(Executor exec, Vfs vfs, Config.Paths paths, Dir cwd, Log print, bool nop)
    {
        var clang_format_path = paths.GetClangFormatExecutable(vfs, cwd, print);
        if (clang_format_path == null)
        {
            return -1;
        }

        var data = ClangFiles.MapAllFilesInRootOnFirstDir(vfs, cwd, FileUtil.IsHeaderOrSource, FileSection.AllFiles);

        foreach (var (project, source_files) in data)
        {
            Printer.Header(project);
            foreach (var file in source_files)
            {
                print.Info(file.GetRelative(cwd));
                if (nop)
                {
                    continue;
                }

                var res = await new ProcessBuilder(clang_format_path, "-i", file.Path)
                    .RunAndGetOutputAsync(exec, cwd);
                if (res.ExitCode != 0)
                {
                    res.PrintOutput(print);
                    return -1;
                }
            }
            print.Info("");
        }

        return 0;
    }
}
