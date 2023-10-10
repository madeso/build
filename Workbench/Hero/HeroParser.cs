using System.Collections.Immutable;
using System.Diagnostics;
using Workbench.Utils;

namespace Workbench.Hero.Parser;

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
        var pathStart = 0;
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
                            pathStart = i;
                            state = States.AngleBracket;
                        }
                        else if (c == "\"")
                        {
                            pathStart = i;
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
                            SystemIncludes.Add(line.Substring(pathStart, i - pathStart - 1));
                            return ParseResult.Ok;
                        }
                        break;
                    case States.Quote:
                        if (c == "\"")
                        {
                            LocalIncludes.Add(line.Substring(pathStart, i - pathStart - 1));
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



internal static class F
{
    internal static string canonicalize_or_default(string p)
    {
        // is this correct?
        return new FileInfo(p).FullName;
    }

    internal static void touch_file(Data.Project project, string abs)
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

    public static Analytics Analyze(Data.Project project)
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

    private void Analyze(string path, Data.Project project)
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

            var isTranslationUnit = FileUtil.IsTranslationUnit(path);

            Analyze(include, project);


            if (FileToData.TryGetValue(include, out var ai))
            {
                ret.AllIncludes.Add(include);
                ai.AllIncludedBy.Add(path);

                if (isTranslationUnit)
                {
                    ai.TranslationUnitsIncludedBy.Add(path);
                }

                var union = ret.AllIncludes.Union(ai.AllIncludes).ToHashSet();
                ret.AllIncludes = union;

                foreach (var inc in ai.AllIncludes)
                {
                    AddAllInc(inc, path);
                    if (isTranslationUnit)
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

    private static void AddFileTable(Html sb, Data.OutputFolders root, string id, string header, IEnumerable<PathCount> count_list)
    {
        sb.PushString($"<div id=\"{id}\">\n");
        sb.PushString($"<a name=\"{id}\"></a>");
        sb.PushString($"<h2>{header}</h2>\n\n");

        sb.PushString("<table class=\"list\">\n");
        foreach (var (pathToFile, count) in count_list)
        {
            var z = Html.inspect_filename_link(root.InputRoot, pathToFile);
            var nf = Core.FormatNumber(count);
            sb.PushString($"  <tr><td class=\"num\">{nf}</td> <td class=\"file\">{z}</td></tr>\n");
        }
        sb.PushString("</table>\n");
        sb.PushString("</div>\n");
    }

    private static string path_to_index_file(string root)
    { return Path.Join(root, "index.html"); }


    public static void GenerateIndexPage(Data.OutputFolders root, Data.Project project, Analytics analytics)
    {
        var sb = new Html();

        sb.BeginJoin("Report");

        // Summary
        {
            var pchLines = project.ScannedFiles
                .Where(kvp => kvp.Value.IsPrecompiled)
                .Select(kvp => kvp.Value.NumberOfLines)
                .Sum();
            var superTotalLines = project.ScannedFiles
                .Select(kvp => kvp.Value.NumberOfLines)
                .Sum();
            var totalLines = superTotalLines - pchLines;
            var totalParsed = analytics.FileToData
                .Where(kvp => FileUtil.IsTranslationUnit(kvp.Key) && !project.ScannedFiles[kvp.Key].IsPrecompiled)
                .Select(kvp => kvp.Value.TotalIncludedLines + project.ScannedFiles[kvp.Key].NumberOfLines)
                .Sum();
            var factor = totalParsed / (double)totalLines;
            var table = new TableRow[]
            {
                new("Files", Core.FormatNumber(project.ScannedFiles.Count)),
                new("Total lines", Core.FormatNumber(totalLines)),
                new("Total precompiled", $"{Core.FormatNumber(pchLines)} (<a href=\"#pch\">list</a>)"),
                new("Total parsed", Core.FormatNumber(totalParsed)),
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
            AddFileTable(sb, root, "largest", "Biggest Contributors", most);
        }

        {
            var hubs = analytics.FileToData
                .Select(kvp => new PathCount(kvp.Key, kvp.Value.AllIncludes.Count * kvp.Value.TranslationUnitsIncludedBy.Count))
                .Where(kvp => kvp.Count > 0)
                .OrderByDescending(kvp => kvp.Count)
                .ToImmutableArray();
            AddFileTable(sb, root, "hubs", "Header Hubs", hubs);
        }

        {
            var pch = project.ScannedFiles
                .Where(kvp => kvp.Value.IsPrecompiled)
                .Select(kvp => new PathCount(kvp.Key, kvp.Value.NumberOfLines))
                .OrderByDescending(kvp => kvp.Count)
                .ToImmutableArray();
            AddFileTable(sb, root, "pch", "Precompiled Headers", pch);
        }

        sb.End();

        sb.WriteToFile(path_to_index_file(root.OutputDirectory));
    }

}


///////////////////////////////////////////////////////////////////////////////////////////////////
// Scanner

public class ProgressFeedback
{
    private readonly Printer _printer;

    public ProgressFeedback(Printer printer)
    {
        _printer = printer;
    }

    public void UpdateTitle(string newTitle)
    {
        _printer.Info($"{newTitle}");
    }

    public void UpdateMessage(string newMessage)
    {
        _printer.Info($"  {newMessage}");
    }

    public void UpdateCount(int newCount)
    {
    }

    public void NextItem()
    {
    }
}

public class Scanner
{
    private bool _isScanningPch = false;

    private readonly HashSet<string> _fileQueue = new();
    private readonly List<string> _scanQueue = new();
    private readonly Dictionary<string, string> _systemIncludes = new();
    public readonly List<string> Errors = new();
    public readonly Dictionary<string, List<string>> NotFoundOrigins = new();
    public readonly ColCounter<string> MissingExt = new();


    public void Rescan(Data.Project project, ProgressFeedback feedback)
    {
        feedback.UpdateTitle("Scanning precompiled header...");
        foreach (var sf in project.ScannedFiles.Values)
        {
            sf.IsTouched = false;
            sf.IsPrecompiled = false;
        }

        // scan everything that goes into precompiled header
        _isScanningPch = true;
        foreach (var inc in project.PrecompiledHeaders)
        {
            if (File.Exists(inc))
            {
                ScanFile(project, inc);
                while (_scanQueue.Count > 0)
                {
                    var toScan = _scanQueue.ToImmutableArray();
                    _scanQueue.Clear();
                    foreach (var fi in toScan)
                    {
                        ScanFile(project, fi);
                    }
                }
                _fileQueue.Clear();
            }
        }
        _isScanningPch = false;

        feedback.UpdateTitle("Scanning directories...");
        foreach (var dir in project.ScanDirectories)
        {
            feedback.UpdateMessage($"{dir}");
            ScanDirectory(dir, feedback);
        }

        feedback.UpdateTitle("Scanning files...");

        var dequeued = 0;

        while (_scanQueue.Count > 0)
        {
            dequeued += _scanQueue.Count;
            var toScan = _scanQueue.ToImmutableArray();
            _scanQueue.Clear();
            foreach (var fi in toScan)
            {
                feedback.UpdateCount(dequeued + _scanQueue.Count);
                feedback.NextItem();
                feedback.UpdateMessage($"{fi}");
                ScanFile(project, fi);
            }
        }
        _fileQueue.Clear();
        _systemIncludes.Clear();

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
            ScanSingleFile(new FileInfo(dir));
            return true;
        }

        var dirInfo = new DirectoryInfo(dir);

        foreach (var file in dirInfo.GetFiles())
        {
            ScanSingleFile(file);
        }

        foreach (var subDir in dirInfo.GetDirectories())
        {
            var dirFullpath = subDir.FullName;
            ScanDirectory(dirFullpath, feedback);
        }

        return true;

        void ScanSingleFile(FileInfo fileInfo)
        {
            var file = fileInfo.FullName;
            var ext = Path.GetExtension(file);
            if (FileUtil.IsTranslationUnitExtension(ext))
            {
                AddToQueue(file, F.canonicalize_or_default(file));
            }
            else
            {
                // printer.info("invalid extension {}", ext);
                MissingExt.AddOne(ext);
            }
        }
    }

    private void AddToQueue(string inc, string abs)
    {
        if (_fileQueue.Contains(abs))
        {
            return;
        }

        _fileQueue.Add(abs);
        _scanQueue.Add(inc);
    }

    private void ScanFile(Data.Project project, string p)
    {
        var path = F.canonicalize_or_default(p);
        // todo(Gustav): add last scan feature!!!
        if (project.ScannedFiles.TryGetValue(path, out var scannedFile)) // && project.LastScan > path.LastWriteTime && !this.is_scanning_pch
        {
            PleaseScanFile(project, path, scannedFile);
            project.ScannedFiles.Add(path, scannedFile);
        }
        else
        {
            var parsed = Result.ParseFile(path, Errors);
            var sourceFile = new Data.SourceFile
            (
                numberOfLines: parsed.NumberOfLines,
                localIncludes: parsed.LocalIncludes,
                systemIncludes: parsed.SystemIncludes,
                isPrecompiled: _isScanningPch
            );
            PleaseScanFile(project, path, sourceFile);
            project.ScannedFiles.Add(path, sourceFile);
        }
    }

    private void PleaseScanFile(Data.Project project, string path, Data.SourceFile sf)
    {
        sf.IsTouched = true;
        sf.AbsoluteIncludes.Clear();

        var localDir = new FileInfo(path).Directory?.FullName;
        if (localDir == null)
        {
            throw new Exception($"{path} does not have a directory");
        }
        foreach (var s in sf.LocalIncludes)
        {
            var inc = Path.Join(localDir, s);
            var abs = F.canonicalize_or_default(inc);
            // found a header that's part of PCH during regular scan: ignore it
            if (!_isScanningPch && project.ScannedFiles.ContainsKey(abs) && project.ScannedFiles[abs].IsPrecompiled)
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
            AddToQueue(inc, abs);
        }

        foreach (var s in sf.SystemIncludes)
        {
            if (_systemIncludes.TryGetValue(s, out var systemInclude))
            {
                // found a header that's part of PCH during regular scan: ignore it
                if (!_isScanningPch && project.ScannedFiles.ContainsKey(systemInclude) && project.ScannedFiles[systemInclude].IsPrecompiled)
                {
                    F.touch_file(project, systemInclude);
                    continue;
                }
                sf.AbsoluteIncludes.Add(systemInclude);
            }
            else
            {
                var foundPath = project.IncludeDirectories
                    .Select(dir => Path.Join(dir, s))
                    .FirstOrDefault(File.Exists);

                if (foundPath != null)
                {
                    var canonicalized = F.canonicalize_or_default(foundPath);
                    // found a header that's part of PCH during regular scan: ignore it
                    if (!_isScanningPch
                        && project.ScannedFiles.TryGetValue(canonicalized, out var scannedFile)
                        && scannedFile.IsPrecompiled)
                    {
                        F.touch_file(project, canonicalized);
                        continue;
                    }

                    sf.AbsoluteIncludes.Add(canonicalized);
                    _systemIncludes.Add(s, canonicalized);
                    AddToQueue(foundPath, canonicalized);
                }
                else if (NotFoundOrigins.TryGetValue(s, out var fileList))
                {
                    fileList.Add(path);
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
