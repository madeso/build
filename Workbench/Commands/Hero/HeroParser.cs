using System.Collections.Immutable;
using System.Diagnostics;
using Spectre.Console;
using Workbench.Shared;
using Workbench.Shared.Extensions;

namespace Workbench.Commands.Hero;

///////////////////////////////////////////////////////////////////////////////////////////////////
// Parser

internal enum States { Start, Hash, Include, AngleBracket, Quote }

internal enum ParseResult { Ok, Error }

public class Result
{
    public readonly List<string> SystemIncludes = new();
    public readonly List<string> LocalIncludes = new();
    public int NumberOfLines = 0;

    private ParseResult ParseLine(string line)
    {
        var i = 0;
        var path_start = 0;
        var state = States.Start;

        while (true)
        {
            if (i >= line.Length)
            {
                return ParseResult.Error;
            }

            var c = line[i..(i + 1)];
            i += 1;

            if (c is " " or "\t")
            {
                // pass
            }
            else
            {
                switch (state)
                {
                    case States.Start:
                        if (c == "#")
                        {
                            state = States.Hash;
                        }
                        else if (c == "/")
                        {
                            if (i >= line.Length)
                            {
                                return ParseResult.Error;
                            }
                            if (line[i..(i + 1)] == "/")
                            {
                                // Matched C++ style comment
                                return ParseResult.Ok;
                            }
                        }
                        else
                        {
                            return ParseResult.Error;
                        }
                        break;
                    case States.Hash:
                        i -= 1;
                        if (line.IndexOf("include", i, StringComparison.InvariantCulture) == i)
                        {
                            i += 7;
                            state = States.Include;
                        }
                        else
                        {
                            // Matched preprocessor other than #include
                            return ParseResult.Ok;
                        }
                        break;
                    case States.Include:
                        if (c == "<")
                        {
                            path_start = i;
                            state = States.AngleBracket;
                        }
                        else if (c == "\"")
                        {
                            path_start = i;
                            state = States.Quote;
                        }
                        else
                        {
                            return ParseResult.Error;
                        }
                        break;
                    case States.AngleBracket:
                        if (c == ">")
                        {
                            SystemIncludes.Add(line.Substring(path_start, i - path_start - 1));
                            return ParseResult.Ok;
                        }
                        break;
                    case States.Quote:
                        if (c == "\"")
                        {
                            LocalIncludes.Add(line.Substring(path_start, i - path_start - 1));
                            return ParseResult.Ok;
                        }
                        break;
                }
            }
        }
    }

    /// Simple parser... only looks foreach #include lines. Does not take #defines or comments into account.
    public static Result ParseFile(string fi, List<string> errors)
    {
        var res = new Result();

        if (File.Exists(fi) == false)
        {
            errors.Add($"Unable to open file {fi}");
            return res;
        }

        var lines = File.ReadAllLines(fi);
        res.NumberOfLines = lines.Length;
        foreach (var line in lines)
        {
            if (line.Contains('#') && line.Contains("include"))
            {
                if (res.ParseLine(line) == ParseResult.Error)
                {
                    errors.Add($"Could not parse line: {line} in file: {fi}");
                }
            }
        }

        return res;
    }
}



internal static class ParserFacade
{
    internal static string canonicalize_or_default(string p)
    {
        // is this correct?
        return new FileInfo(p).FullName;
    }

    internal static void touch_file(Project project, string abs)
    {
        if (project.ScannedFiles.TryGetValue(abs, out var file))
        {
            file.IsTouched = true;
        }
    }

}



///////////////////////////////////////////////////////////////////////////////////////////////////
// Analytics



public class ItemAnalytics
{
    public int TotalIncludedLines = 0;
    public HashSet<string> AllIncludes = new();
    public HashSet<string> AllIncludedBy = new();
    public HashSet<string> TranslationUnitsIncludedBy = new();
    public bool IsAnalyzed = false;
}

public class Analytics
{
    public readonly Dictionary<string, ItemAnalytics> FileToData = new();

    public static Analytics Analyze(Project project)
    {
        var analytics = new Analytics();
        foreach (var file in project.ScannedFiles.Keys)
        {
            analytics.Analyze(file, project);
        }
        return analytics;
    }

    private void AddTrans(string inc, string path)
    {
        if (FileToData.TryGetValue(inc, out var it))
        {
            it.TranslationUnitsIncludedBy.Add(path);
        }
    }

    private void AddAllInc(string inc, string path)
    {
        if (FileToData.TryGetValue(inc, out var it))
        {
            it.AllIncludedBy.Add(path);
        }
    }

    private void Analyze(string path, Project project)
    {
        if (FileToData.TryGetValue(path, out var analytics))
        {
            Debug.Assert(analytics.IsAnalyzed);
            return;
        }

        var ret = new ItemAnalytics
        {
            IsAnalyzed = true
        };

        var sf = project.ScannedFiles[path];
        foreach (var include in sf.AbsoluteIncludes)
        {
            if (include == path) { continue; }

            var is_translation_unit = FileUtil.IsTranslationUnit(new FileInfo(path));

            Analyze(include, project);


            if (FileToData.TryGetValue(include, out var ai))
            {
                ret.AllIncludes.Add(include);
                ai.AllIncludedBy.Add(path);

                if (is_translation_unit)
                {
                    ai.TranslationUnitsIncludedBy.Add(path);
                }

                var union = ret.AllIncludes.Union(ai.AllIncludes).ToHashSet();
                ret.AllIncludes = union;

                foreach (var inc in ai.AllIncludes)
                {
                    AddAllInc(inc, path);
                    if (is_translation_unit)
                    {
                        AddTrans(inc, path);
                    }
                }
            }
            else
            {
                throw new Exception("invalid state?");
            }
        }

        ret.TotalIncludedLines = ret.AllIncludes.Select(f => project.ScannedFiles[f].NumberOfLines).Sum();

        FileToData.Add(path, ret);
    }
}



///////////////////////////////////////////////////////////////////////////////////////////////////
// Report

internal record TableRow(string Label, string Value);

internal record PathCount(string Path, int Count);

public static class Report
{
#if false
    static void order_by_descending(v: List.<(string, int)>)
    {
        v.sort_by_key(kvp => { kvp.Value});
        v.reverse();
    }
#endif

    private static void AddProjectTableSummary(Html sb, IEnumerable<TableRow> table)
    {
        sb.PushString("<div id=\"summary\">\n");
        sb.PushString("<table class=\"summary\">\n");
        foreach (var row in table)
        {
            sb.PushString($"  <tr><th>{row.Label}:</th> <td>{row.Value}</td></tr>\n");
        }
        sb.PushString("</table>\n");
        sb.PushString("</div>\n");
    }

    private static void AddFileTable(
        string? common, Html sb, OutputFolders root, string id, string header, IEnumerable<PathCount> count_list)
    {
        sb.PushString($"<div id=\"{id}\">\n");
        sb.PushString($"<a name=\"{id}\"></a>");
        sb.PushString($"<h2>{header}</h2>\n\n");

        sb.PushString("<table class=\"list\">\n");
        foreach (var (path_to_file, count) in count_list)
        {
            var z = Html.inspect_filename_link(common, root.InputRoot, path_to_file);
            var nf = Core.FormatNumber(count);
            sb.PushString($"  <tr><td class=\"num\">{nf}</td> <td class=\"file\">{z}</td></tr>\n");
        }
        sb.PushString("</table>\n");
        sb.PushString("</div>\n");
    }

    private static string GetPathToIndexFile(string root)
        => Path.Join(root, "index.html");


    public static void GenerateIndexPage(string? common, OutputFolders root, Project project, Analytics analytics)
    {
        var sb = new Html();

        sb.BeginJoin("Report");

        // Summary
        {
            var pch_lines = project.ScannedFiles
                .Where(kvp => kvp.Value.IsPrecompiled)
                .Select(kvp => kvp.Value.NumberOfLines)
                .Sum();
            var super_total_lines = project.ScannedFiles
                .Select(kvp => kvp.Value.NumberOfLines)
                .Sum();
            var total_lines = super_total_lines - pch_lines;
            var total_parsed = analytics.FileToData
                .Where(kvp => FileUtil.IsTranslationUnit(new FileInfo(kvp.Key)) && !project.ScannedFiles[kvp.Key].IsPrecompiled)
                .Select(kvp => kvp.Value.TotalIncludedLines + project.ScannedFiles[kvp.Key].NumberOfLines)
                .Sum();
            var factor = total_parsed / (double)total_lines;
            var table = new TableRow[]
            {
                new("Files", Core.FormatNumber(project.ScannedFiles.Count)),
                new("Total lines", Core.FormatNumber(total_lines)),
                new("Total precompiled", $"{Core.FormatNumber(pch_lines)} (<a href=\"#pch\">list</a>)"),
                new("Total parsed", Core.FormatNumber(total_parsed)),
                new("Blowup factor", $"{factor:0.00} (<a href=\"#largest\">largest</a>, <a href=\"#hubs\">hubs</a>)"),
            };
            AddProjectTableSummary(sb, table);
        }

        {
            var most = analytics.FileToData
                .Select(kvp => new PathCount(kvp.Key, project.ScannedFiles[kvp.Key].NumberOfLines * kvp.Value.TranslationUnitsIncludedBy.Count))
                .Where(kvp => !project.ScannedFiles[kvp.Path].IsPrecompiled)
                .Where(kvp => kvp.Count > 0)
                .OrderByDescending(kvp => kvp.Count)
                .ToImmutableArray();
            AddFileTable(common, sb, root, "largest", "Biggest Contributors", most);
        }

        {
            var hubs = analytics.FileToData
                .Select(kvp => new PathCount(kvp.Key, kvp.Value.AllIncludes.Count * kvp.Value.TranslationUnitsIncludedBy.Count))
                .Where(kvp => kvp.Count > 0)
                .OrderByDescending(kvp => kvp.Count)
                .ToImmutableArray();
            AddFileTable(common, sb, root, "hubs", "Header Hubs", hubs);
        }

        {
            var pch = project.ScannedFiles
                .Where(kvp => kvp.Value.IsPrecompiled)
                .Select(kvp => new PathCount(kvp.Key, kvp.Value.NumberOfLines))
                .OrderByDescending(kvp => kvp.Count)
                .ToImmutableArray();
            AddFileTable(common, sb, root, "pch", "Precompiled Headers", pch);
        }

        sb.End();

        sb.WriteToFile(GetPathToIndexFile(root.OutputDirectory));
    }

}


///////////////////////////////////////////////////////////////////////////////////////////////////
// Scanner

public class ProgressFeedback
{
    public void UpdateTitle(string new_title)
    {
        AnsiConsole.WriteLine($"{new_title}");
    }

    public void UpdateMessage(string new_message)
    {
        AnsiConsole.WriteLine($"  {new_message}");
    }

    public void UpdateCount(int new_count)
    {
    }

    public void NextItem()
    {
    }
}

public class Scanner
{
    private bool is_scanning_pch = false;

    private readonly HashSet<string> file_queue = new();
    private readonly List<string> scan_queue = new();
    private readonly Dictionary<string, string> system_includes = new();

    public readonly List<string> Errors = new();
    public readonly Dictionary<string, List<string>> NotFoundOrigins = new();
    public readonly ColCounter<string> MissingExt = new();


    public void Rescan(Project project, ProgressFeedback feedback)
    {
        feedback.UpdateTitle("Scanning precompiled header...");
        foreach (var sf in project.ScannedFiles.Values)
        {
            sf.IsTouched = false;
            sf.IsPrecompiled = false;
        }

        // scan everything that goes into precompiled header
        is_scanning_pch = true;
        foreach (var inc in project.PrecompiledHeaders)
        {
            if (File.Exists(inc))
            {
                ScanFile(project, inc);
                while (scan_queue.Count > 0)
                {
                    var to_scan = scan_queue.ToImmutableArray();
                    scan_queue.Clear();
                    foreach (var fi in to_scan)
                    {
                        ScanFile(project, fi);
                    }
                }
                file_queue.Clear();
            }
        }
        is_scanning_pch = false;

        feedback.UpdateTitle("Scanning directories...");
        foreach (var dir in project.ScanDirectories)
        {
            feedback.UpdateMessage($"{dir}");
            ScanDirectory(dir, feedback);
        }

        feedback.UpdateTitle("Scanning files...");

        var dequeued = 0;

        while (scan_queue.Count > 0)
        {
            dequeued += scan_queue.Count;
            var to_scan = scan_queue.ToImmutableArray();
            scan_queue.Clear();
            foreach (var fi in to_scan)
            {
                feedback.UpdateCount(dequeued + scan_queue.Count);
                feedback.NextItem();
                feedback.UpdateMessage($"{fi}");
                ScanFile(project, fi);
            }
        }
        file_queue.Clear();
        system_includes.Clear();

        project.ScannedFiles.RemoveAll(kvp => kvp.Value.IsTouched == false);
    }

    private void ScanDirectory(string dir, ProgressFeedback feedback)
    {
        if (PleaseScanDirectory(dir, feedback) == false)
        {
            Errors.Add($"Cannot descend into {dir}");
        }
    }

    private bool PleaseScanDirectory(string dir, ProgressFeedback feedback)
    {
        feedback.UpdateMessage($"{dir}");

        if (File.Exists(dir))
        {
            scan_single_file(new FileInfo(dir));
            return true;
        }

        var dir_info = new DirectoryInfo(dir);

        foreach (var file in dir_info.GetFiles())
        {
            scan_single_file(file);
        }

        foreach (var sub_dir in dir_info.GetDirectories())
        {
            var dir_full_path = sub_dir.FullName;
            ScanDirectory(dir_full_path, feedback);
        }

        return true;

        void scan_single_file(FileInfo file_info)
        {
            var file = file_info.FullName;
            if (FileUtil.IsTranslationUnit(file_info))
            {
                AddToQueue(file, ParserFacade.canonicalize_or_default(file));
            }
            else
            {
                // printer.info("invalid extension {}", ext);
                MissingExt.AddOne(file_info.Extension);
            }
        }
    }

    private void AddToQueue(string inc, string abs)
    {
        if (file_queue.Contains(abs))
        {
            return;
        }

        file_queue.Add(abs);
        scan_queue.Add(inc);
    }

    private void ScanFile(Project project, string p)
    {
        var path = ParserFacade.canonicalize_or_default(p);
        // todo(Gustav): add last scan feature!!!
        if (project.ScannedFiles.TryGetValue(path, out var scanned_file)) // && project.LastScan > path.LastWriteTime && !this.is_scanning_pch
        {
            PleaseScanFile(project, path, scanned_file);
            project.ScannedFiles.Add(path, scanned_file);
        }
        else
        {
            var parsed = Result.ParseFile(path, Errors);
            var source_file = new SourceFile
            (
                number_of_lines: parsed.NumberOfLines,
                local_includes: parsed.LocalIncludes,
                system_includes: parsed.SystemIncludes,
                is_precompiled: is_scanning_pch
            );
            PleaseScanFile(project, path, source_file);
            project.ScannedFiles.Add(path, source_file);
        }
    }

    private void PleaseScanFile(Project project, string path, SourceFile sf)
    {
        sf.IsTouched = true;
        sf.AbsoluteIncludes.Clear();

        var local_dir = new FileInfo(path).Directory?.FullName;
        if (local_dir == null)
        {
            throw new Exception($"{path} does not have a directory");
        }
        foreach (var s in sf.LocalIncludes)
        {
            var inc = Path.Join(local_dir, s);
            var abs = ParserFacade.canonicalize_or_default(inc);
            // found a header that's part of PCH during regular scan: ignore it
            if (!is_scanning_pch && project.ScannedFiles.ContainsKey(abs) && project.ScannedFiles[abs].IsPrecompiled)
            {
                ParserFacade.touch_file(project, abs);
                continue;
            }
            if (!Path.Exists(inc))
            {
                if (!sf.SystemIncludes.Contains(s))
                {
                    sf.SystemIncludes.Add(s);
                }
                continue;
            }
            sf.AbsoluteIncludes.Add(abs);
            AddToQueue(inc, abs);
        }

        foreach (var s in sf.SystemIncludes)
        {
            if (system_includes.TryGetValue(s, out var system_include))
            {
                // found a header that's part of PCH during regular scan: ignore it
                if (!is_scanning_pch && project.ScannedFiles.ContainsKey(system_include) && project.ScannedFiles[system_include].IsPrecompiled)
                {
                    ParserFacade.touch_file(project, system_include);
                    continue;
                }
                sf.AbsoluteIncludes.Add(system_include);
            }
            else
            {
                var found_path = project.IncludeDirectories
                    .Select(dir => Path.Join(dir, s))
                    .FirstOrDefault(File.Exists);

                if (found_path != null)
                {
                    var canonicalized = ParserFacade.canonicalize_or_default(found_path);
                    // found a header that's part of PCH during regular scan: ignore it
                    if (!is_scanning_pch
                        && project.ScannedFiles.TryGetValue(canonicalized, out var scanned_file)
                        && scanned_file.IsPrecompiled)
                    {
                        ParserFacade.touch_file(project, canonicalized);
                        continue;
                    }

                    sf.AbsoluteIncludes.Add(canonicalized);
                    system_includes.Add(s, canonicalized);
                    AddToQueue(found_path, canonicalized);
                }
                else if (NotFoundOrigins.TryGetValue(s, out var file_list))
                {
                    file_list.Add(path);
                }
                else
                {
                    NotFoundOrigins.Add(s, new() { path });
                }
            }
        }

        // Only treat each include as done once. Since we completely ignore preprocessor, foreach patterns like
        // this we'd end up having same file in includes list multiple times. Let's assume that all includes use
        // pragma once or include guards and are only actually parsed just once.
        //   #if FOO
        //   #include <bar>
        //   #else
        //   #include <bar>
        //   #endif
        sf.AbsoluteIncludes = sf.AbsoluteIncludes.ToImmutableHashSet().ToList();
    }
}
