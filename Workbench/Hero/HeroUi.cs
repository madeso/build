using Workbench.Hero.Data;
using Workbench.Utils;

namespace Workbench.Hero;

///////////////////////////////////////////////////////////////////////////////////////////////////
// MainForm


internal static class Ui
{

    public static void ScanAndGenerateHtml(Printer printer, Data.UserInput input, Data.OutputFolders root)
    {
        var project = new Data.Project(input);
        var scanner = new Parser.Scanner();
        var feedback = new Parser.ProgressFeedback(printer);
        scanner.Rescan(project, feedback);
        var f = new UniqueFiles();
        AddFiles(f, project);
        GenerateReport(f.GetCommon(), root, project, scanner);
    }

    private static void AddFiles(UniqueFiles uniqueFiles, Project project)
    {
        foreach(var k in project.ScannedFiles.Keys)
        {
            uniqueFiles.Add(k);
        }
    }

    public static void ScanAndGenerateDot(Printer printer, Data.UserInput input, Data.OutputFolders root, bool simplifyGraphviz, bool onlyHeaders, string[] exclude, bool cluster)
    {
        var project = new Data.Project(input);
        var scanner = new Parser.Scanner();
        var feedback = new Parser.ProgressFeedback(printer);
        scanner.Rescan(project, feedback);
        var f = new UniqueFiles();
        AddFiles(f, project);
        GenerateDot(printer, f.GetCommon(), root, project, scanner, simplifyGraphviz, onlyHeaders, exclude, input, cluster);
    }


    ///////////////////////////////////////////////////////////////////////////////////////////////////
    // ReportForm

    private static bool FileIsInFileList(string f, IEnumerable<string> list)
    {
        var ff = new FileInfo(f);
        return list.Select(p => new FileInfo(p)).Any(pp => ff.FullName == pp.FullName);
    }

    private static bool ExcludeFile(string file, Data.UserInput input, bool onlyHeaders, IEnumerable<string> exclude)
    {
        if (FileIsInFileList(file, input.ProjectDirectories))
        {
            // explicit included... then it's not excluded
            return false;
        }
        else if (onlyHeaders && FileUtil.IsHeader(file) == false)
        {
            return true;
        }
        else if (FileIsInFileList(file, exclude))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private static void GenerateDot(Printer print, string? common, Data.OutputFolders root, Data.Project project, Parser.Scanner scanner, bool simplifyGraphviz, bool onlyHeaders, string[] exclude, Data.UserInput input, bool cluster)
    {
        var analytics = Parser.Analytics.Analyze(project);
        var gv = new Graphviz();

        foreach (var file in project.ScannedFiles.Keys)
        {
            if (ExcludeFile(file, input, onlyHeaders, exclude))
            {
                Printer.Info($"{file} rejected due to non-header");
                continue;
            }

            Printer.Info($"{file} added as a node");
            var displayName = Html.GetFilename(common, root.InputRoot, file);
            var nodeId = Html.GetSafeInspectFilenameWithoutHtml(file);
            var addedNode = gv.AddNodeWithId(displayName, Shape.Box, nodeId);
            if (!cluster) continue;

            var parent = new FileInfo(file).Directory?.FullName;
            if (parent != null)
            {
                addedNode.Cluster = gv.FindOrCreateCluster(Path.GetRelativePath(root.InputRoot, parent));
            }
        }

        foreach (var file in project.ScannedFiles.Keys)
        {
            if (ExcludeFile(file, input, onlyHeaders, exclude))
            {
                Printer.Info($"{file} rejected due to non-header");
                continue;
            }

            var fromFile = Html.GetSafeInspectFilenameWithoutHtml(file);
            var fromId = gv.GetNodeFromId(fromFile);
            if (fromId == null)
            {
                throw new Exception("BUG: Node not added");
            }

            foreach (var s in project.ScannedFiles[file].AbsoluteIncludes)
            {
                if (ExcludeFile(s, input, onlyHeaders, exclude))
                {
                    Printer.Info($"{s} rejected due to non-header");
                    continue;
                }

                var toFile = Html.GetSafeInspectFilenameWithoutHtml(s);
                var toId = gv.GetNodeFromId(toFile);
                if (toId == null)
                {
                    throw new Exception("BUG: Node not added");
                }

                // todo(Gustav): add edge label
                // let display_count    = rust::num_format(length_fun(&analytics.file_to_data[s]));
                // let display_lines    = rust::num_format(analytics.file_to_data[s].total_included_lines);
                gv.AddEdge(fromId, toId);
            }
        }

        if (simplifyGraphviz)
        {
            gv.Simplify();
        }

        gv.WriteFile(root.OutputDirectory);
    }

    private static void GenerateReport(string? common, Data.OutputFolders root, Data.Project project, Parser.Scanner scanner)
    {
        {
            var html = new Html();
            html.BeginJoin("Errors");
            foreach (var s in scanner.Errors)
            {
                html.PushString($"<p>{s}</p>");
            }
            html.PushString("<h1>Unhandled extensions</h1>");
            foreach (var (ext, count) in scanner.MissingExt.MostCommon())
            {
                html.PushString($"<p>{ext} {count}</p>");
            }
            html.End();

            var path = Path.Join(root.OutputDirectory, "errors.html");
            html.WriteToFile(path);
        }

        {
            var html = new Html();
            html.BeginJoin("Missing");
            // todo(Gustav): sort missing on paths or count?
            foreach (var (includePath, origins) in scanner.NotFoundOrigins)
            {
                var count = origins.Count;
                var s = count == 1 ? "" : "s";
                html.PushString($"<div class=\"missing\">{includePath} from <span class=\"num\">{count}</span> file{s} <ul>");

                foreach (var file in origins)
                {
                    html.PushString($"<li>{Html.inspect_filename_link(common, root.InputRoot, file)}</li>");
                }
                html.PushString("</ul></div>");

            }
            html.End();

            var path = Path.Join(root.OutputDirectory, "missing.html");
            html.WriteToFile(path);
        }

        var analytics = Parser.Analytics.Analyze(project);
        Html.WriteCssFile(root.OutputDirectory);
        Parser.Report.GenerateIndexPage(common, root, project, analytics);

        foreach (var f in project.ScannedFiles.Keys)
        {
            write_inspection_page(common, root, f, project, analytics);
        }
    }

    private static void WriteInspectHeaderTable
    (
        string? common,
        Html html,
        Data.OutputFolders root,
        Parser.Analytics analytics,
        IEnumerable<string> included,
        string klass,
        string header,
        Func<Parser.ItemAnalytics, int> lengthFun
    )
    {
        html.PushString($"<div id=\"{klass}\">\n");
        html.PushString($"<h2>{header}</h2>\n");
        html.PushString("<table class=\"list\">\n");
        html.PushString("<tr>  <th class=\"file\">File</th>  <th>Count</th>  <th>Lines</th>  </tr>\n");

        foreach (var s in included.OrderByDescending(s => lengthFun(analytics.FileToData[s])))
        {
            var displayFilename = Html.inspect_filename_link(common, root.InputRoot, s);
            var displayCount = Core.FormatNumber(lengthFun(analytics.FileToData[s]));
            var displayLines = Core.FormatNumber(analytics.FileToData[s].TotalIncludedLines);

            html.PushString($"<tr><td class=\"file\">{displayFilename}</td> <td class=\"num\">{displayCount}</td> <td class=\"num\">{displayLines}</td></tr>");
        }

        html.PushString("</table>\n");
        html.PushString("</div>\n");
    }

    private static void write_inspection_page(string? common, Data.OutputFolders root, string file, Data.Project project, Parser.Analytics analytics)
    {
        var html = new Html();

        var displayName = Html.GetFilename(common, root.InputRoot, file);

        html.BeginJoin($"Inspecting - {displayName}");

        WriteInspectHeaderTable
        (
            common, html, root,
            analytics,
            project.ScannedFiles
                .Where(kvp => kvp.Value.AbsoluteIncludes.Contains(file))
                .Select(kvp => kvp.Key),
            "included_by", $"These include {displayName}",
            it => it.AllIncludedBy.Count
        );

        {
            html.PushString("<div id=\"file\">\n");

            var projectFile = project.ScannedFiles[file];
            var analyticsFile = analytics.FileToData[file];
            var fileLines = Core.FormatNumber(projectFile.NumberOfLines);
            var directLines = Core.FormatNumber(projectFile.AbsoluteIncludes.Select(f => project.ScannedFiles[f].NumberOfLines).Sum());
            var directCount = Core.FormatNumber(projectFile.AbsoluteIncludes.Count);
            var totalLines = Core.FormatNumber(analyticsFile.TotalIncludedLines);
            var totalCount = Core.FormatNumber(analyticsFile.AllIncludes.Count);

            html.PushString($"<h2>{displayName}</h2>\n");

            html.PushString("<table class=\"summary\">");

            html.PushString("<tr>  <th></th>                 <th>Lines</th>              <th>Files</th>           </tr>\n");
            html.PushString($"<tr>  <th>Lines:</th>           <td class=\"num\">{fileLines}</td>   <td class=\"num\">1</td> </tr>\n");
            html.PushString($"<tr>  <th>Direct Includes:</th> <td class=\"num\">{directLines}</td>  <td class=\"num\">{directCount}</td>  </tr>\n");
            html.PushString($"<tr>  <th>Total Includes:</th>  <td class=\"num\">{totalLines}</td>  <td class=\"num\">{totalCount}</td> </tr>\n");

            html.PushString("</table>");

            html.PushString("</div>\n");
        }

        WriteInspectHeaderTable
        (
            common, html, root,
            analytics,
            project.ScannedFiles[file].AbsoluteIncludes,
            "includes", $"{displayName} includes these",
            it => it.AllIncludes.Count
        );

        html.End();

        var filename = Html.GetSafeInspectFilenameHtml(file);
        var path = Path.Join(root.OutputDirectory, filename);
        html.WriteToFile(path);
    }

}


internal static class F
{
    internal static int HandleNewHero(string projectFile, bool overwrite, Printer print)
    {
        if (File.Exists(projectFile) && overwrite == false)
        {
            print.Error($"{projectFile} already exists.");
            return -1;
        }
        var input = new Data.UserInput();
        input.IncludeDirectories.Add("list of relative or absolute directories");
        input.ProjectDirectories.Add("list of relative or absolute source directories (or files)");
        input.PrecompiledHeaders.Add("list of relative pchs, if there are any");

        var content = JsonUtil.Write(input);
        File.WriteAllText(projectFile, content);
        return 0;
    }

    internal static int HandleRunHeroHtml(string projectFile, string outputDirectory, Printer print)
    {
        var input = Data.UserInput.LoadFromFile(print, projectFile);
        if (input == null)
        {
            return -1;
        }
        if (input.Validate(print) == false)
        {
            return -1;
        }
        var inputRoot = new FileInfo(projectFile).DirectoryName ?? Environment.CurrentDirectory;
        input.Decorate(print, inputRoot);
        Directory.CreateDirectory(outputDirectory);
        Ui.ScanAndGenerateHtml(print, input, new(inputRoot, outputDirectory));
        return 0;
    }

    internal static int RunHeroGraphviz(string projectFile,
        string outputFile,
        bool simplifyGraphviz,
        bool onlyHeaders,
        bool cluster,
        string[] exclude, Printer print)
    {
        var input = Data.UserInput.LoadFromFile(print, projectFile);
        if (input == null)
        {
            return -1;
        }
        if (input.Validate(print) == false)
        {
            return -1;
        }
        var inputRoot = new FileInfo(projectFile).DirectoryName ?? Environment.CurrentDirectory;
        input.Decorate(print, inputRoot);
        Ui.ScanAndGenerateDot(print, input, new(inputRoot, outputFile), simplifyGraphviz, onlyHeaders, exclude, cluster);
        return 0;
    }
}

