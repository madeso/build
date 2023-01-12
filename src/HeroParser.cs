﻿using System.Collections.Immutable;
using System.Diagnostics;

namespace Workbench.Hero.Parser;

///////////////////////////////////////////////////////////////////////////////////////////////////
// Parser

enum States { Start, Hash, Include, AngleBracket, Quote }
enum ParseResult { Ok, Error }

public class Result
{
    public readonly List<string> system_includes = new();
    public readonly List<string> local_includes = new();
    public int number_of_lines = 0;

    ParseResult parse_line(string line)
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

            if (c == " " || c == "\t")
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
                        if (line.IndexOf("include", i) == i)
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
                            this.system_includes.Add(line.Substring(path_start, i - path_start - 1));
                            return ParseResult.Ok;
                        }
                        break;
                    case States.Quote:
                        if (c == "\"")
                        {
                            this.local_includes.Add(line.Substring(path_start, i - path_start - 1));
                            return ParseResult.Ok;
                        }
                        break;
                }
            }
        }
    }

    /// Simple parser... only looks foreach #include lines. Does not take #defines or comments into account.
    public static Result parse_file(string fi, List<string> errors)
    {
        var res = new Result();

        if (File.Exists(fi) == false)
        {
            errors.Add($"Unable to open file {fi}");
            return res;
        }

        var lines = File.ReadAllLines(fi);
        res.number_of_lines = lines.Length;
        foreach (var line in lines)
        {
            if (line.Contains('#') && line.Contains("include"))
            {
                if (res.parse_line(line) == ParseResult.Error)
                {
                    errors.Add($"Could not parse line: {line} in file: {fi}");
                }
            }
        }

        return res;
    }
}



internal static class F
{
    internal static string canonicalize_or_default(string p)
    {
        // is this correct?
        return new FileInfo(p).FullName;
    }

    internal static void touch_file(Data.Project project, string abs)
    {
        if (project.scanned_files.TryGetValue(abs, out var file))
        {
            file.IsTouched = true;
        }
    }

}



///////////////////////////////////////////////////////////////////////////////////////////////////
// Analytics



public class ItemAnalytics
{
    public int total_included_lines = 0;
    public HashSet<string> all_includes = new();
    public HashSet<string> all_included_by = new();
    public HashSet<string> translation_units_included_by = new();
    public bool is_analyzed = false;
}

public class Analytics
{
    public readonly Dictionary<string, ItemAnalytics> file_to_data = new();

    public static Analytics analyze(Data.Project project)
    {
        var analytics = new Analytics();
        foreach (var file in project.scanned_files.Keys)
        {
            analytics.analyze(file, project);
        }
        return analytics;
    }

    void add_trans(string inc, string path)
    {
        if (this.file_to_data.TryGetValue(inc, out var it))
        {
            it.translation_units_included_by.Add(path);
        }
    }

    void add_all_inc(string inc, string path)
    {
        if (this.file_to_data.TryGetValue(inc, out var it))
        {
            it.all_included_by.Add(path);
        }
    }

    void analyze(string path, Data.Project project)
    {
        if (this.file_to_data.TryGetValue(path, out var reta))
        {
            Debug.Assert(reta.is_analyzed);
            return;
        }

        var ret = new ItemAnalytics
        {
            is_analyzed = true
        };

        var sf = project.scanned_files[path];
        foreach (var include in sf.AbsoluteIncludes)
        {
            if (include == path) { continue; }

            var is_tu = Data.Utils.IsTranslationUnit(path);

            this.analyze(include, project);


            if (this.file_to_data.TryGetValue(include, out var ai))
            {
                ret.all_includes.Add(include);
                ai.all_included_by.Add(path);

                if (is_tu)
                {
                    ai.translation_units_included_by.Add(path);
                }

                var union = ret.all_includes.Union(ai.all_includes).ToHashSet();
                ret.all_includes = union;

                foreach (var inc in ai.all_includes)
                {
                    this.add_all_inc(inc, path);
                    if (is_tu)
                    {
                        this.add_trans(inc, path);
                    }
                }
            }
            else
            {
                throw new Exception("invalid state?");
            }
        }

        ret.total_included_lines = ret.all_includes.Select(f => project.scanned_files[f].NumberOfLines).Sum();

        this.file_to_data.Add(path, ret);
    }
}



///////////////////////////////////////////////////////////////////////////////////////////////////
// Report

record TableRow(string Label, string Value);
record PathCount(string Path, int Count);

public static class Report
{
#if false
    static void order_by_descending(v: List.<(string, int)>)
    {
        v.sort_by_key(kvp => { kvp.Value});
        v.reverse();
    }
#endif

    static void add_project_table_summary(Html sb, IEnumerable<TableRow> table)
    {
        sb.push_str("<div id=\"summary\">\n");
        sb.push_str("<table class=\"summary\">\n");
        foreach (var row in table)
        {
            sb.push_str($"  <tr><th>{row.Label}:</th> <td>{row.Value}</td></tr>\n");
        }
        sb.push_str("</table>\n");
        sb.push_str("</div>\n");
    }

    static void add_file_table(Html sb, Data.OutputFolders root, string id, string header, IEnumerable<PathCount> count_list)
    {
        sb.push_str($"<div id=\"{id}\">\n");
        sb.push_str($"<a name=\"{id}\"></a>");
        sb.push_str($"<h2>{header}</h2>\n\n");

        sb.push_str("<table class=\"list\">\n");
        foreach (var (path_to_file, count) in count_list)
        {
            var z = Html.inspect_filename_link(root.InputRoot, path_to_file);
            var nf = Core.FormatNumber(count);
            sb.push_str($"  <tr><td class=\"num\">{nf}</td> <td class=\"file\">{z}</td></tr>\n");
        }
        sb.push_str("</table>\n");
        sb.push_str("</div>\n");
    }



    static string path_to_index_file(string root)
    { return Path.Join(root, "index.html"); }


    public static void generate_index_page(Data.OutputFolders root, Data.Project project, Analytics analytics)
    {
        var sb = new Html();

        sb.begin("Report");

        // Summary
        {
            var pch_lines = project.scanned_files
                .Where(kvp => kvp.Value.IsPrecompiled)
                .Select(kvp => kvp.Value.NumberOfLines)
                .Sum();
            var super_total_lines = project.scanned_files
                .Select(kvp => kvp.Value.NumberOfLines)
                .Sum();
            var total_lines = super_total_lines - pch_lines;
            var total_parsed = analytics.file_to_data
                .Where(kvp => Data.Utils.IsTranslationUnit(kvp.Key) && !project.scanned_files[kvp.Key].IsPrecompiled)
                .Select(kvp => kvp.Value.total_included_lines + project.scanned_files[kvp.Key].NumberOfLines)
                .Sum();
            var factor = (double)total_parsed / (double)total_lines;
            var table = new TableRow[]
            {
                new TableRow("Files", Core.FormatNumber(project.scanned_files.Count)),
                new TableRow("Total lines", Core.FormatNumber(total_lines)),
                new TableRow("Total precompiled", $"{Core.FormatNumber(pch_lines)} (<a href=\"#pch\">list</a>)"),
                new TableRow("Total parsed", Core.FormatNumber(total_parsed)),
                new TableRow("Blowup factor", $"{factor:0.00} (<a href=\"#largest\">largest</a>, <a href=\"#hubs\">hubs</a>)"),
            };
            add_project_table_summary(sb, table);
        }

        {
            var most = analytics.file_to_data
                .Select(kvp => new PathCount(kvp.Key, project.scanned_files[kvp.Key].NumberOfLines * kvp.Value.translation_units_included_by.Count))
                .Where(kvp => !project.scanned_files[kvp.Path].IsPrecompiled)
                .Where(kvp => kvp.Count > 0)
                .OrderByDescending(kvp => kvp.Count)
                .ToImmutableArray();
            add_file_table(sb, root, "largest", "Biggest Contributors", most);
        }

        {
            var hubs = analytics.file_to_data
                .Select(kvp => new PathCount(kvp.Key, kvp.Value.all_includes.Count * kvp.Value.translation_units_included_by.Count))
                .Where(kvp => kvp.Count > 0)
                .OrderByDescending(kvp => kvp.Count)
                .ToImmutableArray();
            add_file_table(sb, root, "hubs", "Header Hubs", hubs);
        }

        {
            var pch = project.scanned_files
                .Where(kvp => kvp.Value.IsPrecompiled)
                .Select(kvp => new PathCount(kvp.Key, kvp.Value.NumberOfLines))
                .OrderByDescending(kvp => kvp.Count)
                .ToImmutableArray();
            add_file_table(sb, root, "pch", "Precompiled Headers", pch);
        }

        sb.end();

        sb.write_to_file(path_to_index_file(root.OutputDirectory));
    }

}


///////////////////////////////////////////////////////////////////////////////////////////////////
// Scanner

public class ProgressFeedback
{
    private Printer printer;

    public ProgressFeedback(Printer printer)
    {
        this.printer = printer;
    }

    public void update_title(string new_title)
    {
        printer.Info($"{new_title}");
    }

    public void update_message(string new_message)
    {
        printer.Info($"  {new_message}");
    }

    public void update_count(int new_count)
    {
    }

    public void next_item()
    {
    }
}

public class Scanner
{
    private bool is_scanning_pch = false;

    private readonly HashSet<string> file_queue = new();
    private readonly List<string> scan_queue = new();
    private readonly Dictionary<string, string> system_includes = new();
    public readonly List<string> errors = new();
    public readonly Dictionary<string, List<string>> not_found_origins = new();
    public readonly ColCounter<string> missing_ext = new();


    public void rescan(Data.Project project, ProgressFeedback feedback)
    {
        feedback.update_title("Scanning precompiled header...");
        foreach (var sf in project.scanned_files.Values)
        {
            sf.IsTouched = false;
            sf.IsPrecompiled = false;
        }

        // scan everything that goes into precompiled header
        this.is_scanning_pch = true;
        foreach (var inc in project.precompiled_headers)
        {
            if (File.Exists(inc))
            {
                this.scan_file(project, inc);
                while (this.scan_queue.Count > 0)
                {
                    var to_scan = this.scan_queue.ToImmutableArray();
                    this.scan_queue.Clear();
                    foreach (var fi in to_scan)
                    {
                        this.scan_file(project, fi);
                    }
                }
                this.file_queue.Clear();
            }
        }
        this.is_scanning_pch = false;

        feedback.update_title("Scanning directories...");
        foreach (var dir in project.scan_directories)
        {
            feedback.update_message($"{dir}");
            this.scan_directory(dir, feedback);
        }

        feedback.update_title("Scanning files...");

        var dequeued = 0;

        while (this.scan_queue.Count > 0)
        {
            dequeued += this.scan_queue.Count;
            var to_scan = this.scan_queue.ToImmutableArray();
            this.scan_queue.Clear();
            foreach (var fi in to_scan)
            {
                feedback.update_count(dequeued + this.scan_queue.Count);
                feedback.next_item();
                feedback.update_message($"{fi}");
                this.scan_file(project, fi);
            }
        }
        this.file_queue.Clear();
        this.system_includes.Clear();

        project.scanned_files.RemoveAll(kvp => kvp.Value.IsTouched == false);
    }

    void scan_directory(string dir, ProgressFeedback feedback)
    {
        if (this.please_scan_directory(dir, feedback) == false)
        {
            this.errors.Add($"Cannot descend into {dir}");
        }
    }

    bool please_scan_directory(string dir, ProgressFeedback feedback)
    {
        feedback.update_message($"{dir}");

        if (File.Exists(dir))
        {
            ScanSingleFile(new FileInfo(dir));
            return true;
        }

        var dirinfo = new DirectoryInfo(dir);

        foreach (var ffile in dirinfo.GetFiles())
        {
            ScanSingleFile(ffile);
        }

        foreach (var ssubdir in dirinfo.GetDirectories())
        {
            var subdir = ssubdir.FullName;
            this.scan_directory(subdir, feedback);
        }

        return true;

        void ScanSingleFile(FileInfo ffile)
        {
            var file = ffile.FullName;
            var ext = Path.GetExtension(file);
            if (Data.Utils.IsTranslationUnitExtension(ext))
            {
                this.add_to_queue(file, F.canonicalize_or_default(file));
            }
            else
            {
                // printer.info("invalid extension {}", ext);
                this.missing_ext.AddOne(ext);
            }
        }
    }

    void add_to_queue(string inc, string abs)
    {
        if (!this.file_queue.Contains(abs))
        {
            this.file_queue.Add(abs);
            this.scan_queue.Add(inc);
        }
    }

    void scan_file(Data.Project project, string p)
    {
        var path = F.canonicalize_or_default(p);
        // todo(Gustav): add last scan feature!!!
        if (project.scanned_files.ContainsKey(path)) // && project.LastScan > path.LastWriteTime && !this.is_scanning_pch
        {
            var sf = project.scanned_files[path];
            this.please_scan_file(project, path, sf);
            project.scanned_files.Add(path, sf);
        }
        else
        {
            var res = Result.parse_file(path, this.errors);
            var sf = new Data.SourceFile
            (
                numberOfLines: res.number_of_lines,
                localIncludes: res.local_includes,
                systemIncludes: res.system_includes,
                isPrecompiled: this.is_scanning_pch
            );
            this.please_scan_file(project, path, sf);
            project.scanned_files.Add(path, sf);
        }
    }

    void please_scan_file(Data.Project project, string path, Data.SourceFile sf)
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
            var abs = F.canonicalize_or_default(inc);
            // found a header that's part of PCH during regular scan: ignore it
            if (!this.is_scanning_pch && project.scanned_files.ContainsKey(abs) && project.scanned_files[abs].IsPrecompiled)
            {
                F.touch_file(project, abs);
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
            this.add_to_queue(inc, abs);
        }

        foreach (var s in sf.SystemIncludes)
        {
            if (this.system_includes.ContainsKey(s))
            {
                var abs = this.system_includes[s];
                // found a header that's part of PCH during regular scan: ignore it
                if (!this.is_scanning_pch && project.scanned_files.ContainsKey(abs) && project.scanned_files[abs].IsPrecompiled)
                {
                    F.touch_file(project, abs);
                    continue;
                }
                sf.AbsoluteIncludes.Add(abs);
            }
            else
            {
                string? found_path = project.include_directories
                    .Select(dir => Path.Join(dir, s))
                    .Where(f => File.Exists(f))
                    .FirstOrDefault();

                if (found_path != null)
                {
                    var abs = F.canonicalize_or_default(found_path);
                    // found a header that's part of PCH during regular scan: ignore it
                    if (!this.is_scanning_pch && project.scanned_files.ContainsKey(abs) && project.scanned_files[abs].IsPrecompiled)
                    {
                        F.touch_file(project, abs);
                        continue;
                    }

                    sf.AbsoluteIncludes.Add(abs);
                    this.system_includes.Add(s, abs);
                    this.add_to_queue(found_path, abs);
                }
                else if (this.not_found_origins.TryGetValue(s, out var file_list))
                {
                    file_list.Add(path);
                }
                else
                {
                    this.not_found_origins.Add(s, new() { path });
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