using Spectre.Console;
using Workbench.Shared;

namespace Workbench.Commands.Hero;

///////////////////////////////////////////////////////////////////////////////////////////////////
// MainForm


internal static class Ui
{

    public static void ScanAndGenerateHtml(Log log, UserInput input, OutputFolders root)
    {
        var project = new Project(input);
        var scanner = new Scanner();
        var feedback = new ProgressFeedback();
        scanner.Rescan(project, feedback);
        var f = new UniqueFiles();
        AddFiles(f, project);
        GenerateReport(f.GetCommon(), root, project, scanner);
    }

    private static void AddFiles(UniqueFiles unique_files, Project project)
    {
        foreach (var k in project.ScannedFiles.Keys)
        {
            unique_files.Add(k);
        }
    }

    public static void ScanAndGenerateDot(
        Log log, UserInput input, OutputFolders root, bool simplify_graphviz, bool only_headers,
        string[] exclude, bool cluster)
    {
        var project = new Project(input);
        var scanner = new Scanner();
        var feedback = new ProgressFeedback();
        scanner.Rescan(project, feedback);
        var f = new UniqueFiles();
        AddFiles(f, project);
        GenerateDot(f.GetCommon(), root, project, scanner, simplify_graphviz, only_headers, exclude, input, cluster);
    }


    ///////////////////////////////////////////////////////////////////////////////////////////////////
    // ReportForm

    private static bool FileIsInFileList(string f, IEnumerable<string> list)
    {
        var ff = new FileInfo(f);
        return list.Select(p => new FileInfo(p)).Any(pp => ff.FullName == pp.FullName);
    }

    private static bool ExcludeFile(string file, UserInput input, bool only_headers, IEnumerable<string> exclude)
    {
        if (FileIsInFileList(file, input.ProjectDirectories))
        {
            // explicit included... then it's not excluded
            return false;
        }
        else if (only_headers && FileUtil.IsHeader(new FileInfo(file)) == false)
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

    private static void GenerateDot(
        string? common, OutputFolders root, Project project, Scanner scanner,
        bool simplify_graphviz, bool only_headers, string[] exclude, UserInput input, bool cluster)
    {
        var analytics = Analytics.Analyze(project);
        var gv = new Graphviz();

        foreach (var file in project.ScannedFiles.Keys)
        {
            if (ExcludeFile(file, input, only_headers, exclude))
            {
                AnsiConsole.WriteLine($"{file} rejected due to non-header");
                continue;
            }

            AnsiConsole.WriteLine($"{file} added as a node");
            var display_name = Html.GetFilename(common, root.InputRoot, file);
            var node_id = Html.GetSafeInspectFilenameWithoutHtml(file);
            var added_node = gv.AddNodeWithId(display_name, Shape.Box, node_id);
            if (!cluster) continue;

            var parent = new FileInfo(file).Directory?.FullName;
            if (parent != null)
            {
                added_node.Cluster = gv.FindOrCreateCluster(Path.GetRelativePath(root.InputRoot, parent));
            }
        }

        foreach (var file in project.ScannedFiles.Keys)
        {
            if (ExcludeFile(file, input, only_headers, exclude))
            {
                AnsiConsole.WriteLine($"{file} rejected due to non-header");
                continue;
            }

            var from_file = Html.GetSafeInspectFilenameWithoutHtml(file);
            var from_id = gv.GetNodeFromId(from_file);
            if (from_id == null)
            {
                throw new Exception("BUG: Node not added");
            }

            foreach (var s in project.ScannedFiles[file].AbsoluteIncludes)
            {
                if (ExcludeFile(s, input, only_headers, exclude))
                {
                    AnsiConsole.WriteLine($"{s} rejected due to non-header");
                    continue;
                }

                var to_file = Html.GetSafeInspectFilenameWithoutHtml(s);
                var to_id = gv.GetNodeFromId(to_file);
                if (to_id == null)
                {
                    throw new Exception("BUG: Node not added");
                }

                // todo(Gustav): add edge label
                // let display_count    = rust::num_format(length_fun(&analytics.file_to_data[s]));
                // let display_lines    = rust::num_format(analytics.file_to_data[s].total_included_lines);
                gv.AddEdge(from_id, to_id);
            }
        }

        if (simplify_graphviz)
        {
            gv.Simplify();
        }

        gv.WriteFile(root.OutputDirectory);
    }

    private static void GenerateReport(string? common, OutputFolders root, Project project, Scanner scanner)
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
            foreach (var (include_path, origins) in scanner.NotFoundOrigins)
            {
                var count = origins.Count;
                var s = count == 1 ? "" : "s";
                html.PushString($"<div class=\"missing\">{include_path} from <span class=\"num\">{count}</span> file{s} <ul>");

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

        var analytics = Analytics.Analyze(project);
        Html.WriteCssFile(root.OutputDirectory);
        Report.GenerateIndexPage(common, root, project, analytics);

        foreach (var f in project.ScannedFiles.Keys)
        {
            write_inspection_page(common, root, f, project, analytics);
        }
    }

    private static void WriteInspectHeaderTable
    (
        string? common,
        Html html,
        OutputFolders root,
        Analytics analytics,
        IEnumerable<string> included,
        string klass,
        string header,
        Func<ItemAnalytics, int> length_fun
    )
    {
        html.PushString($"<div id=\"{klass}\">\n");
        html.PushString($"<h2>{header}</h2>\n");
        html.PushString("<table class=\"list\">\n");
        html.PushString("<tr>  <th class=\"file\">File</th>  <th>Count</th>  <th>Lines</th>  </tr>\n");

        foreach (var s in included.OrderByDescending(s => length_fun(analytics.FileToData[s])))
        {
            var display_filename = Html.inspect_filename_link(common, root.InputRoot, s);
            var display_count = Core.FormatNumber(length_fun(analytics.FileToData[s]));
            var display_lines = Core.FormatNumber(analytics.FileToData[s].TotalIncludedLines);

            html.PushString($"<tr><td class=\"file\">{display_filename}</td> <td class=\"num\">{display_count}</td> <td class=\"num\">{display_lines}</td></tr>");
        }

        html.PushString("</table>\n");
        html.PushString("</div>\n");
    }

    private static void write_inspection_page(string? common, OutputFolders root, string file, Project project, Analytics analytics)
    {
        var html = new Html();

        var display_name = Html.GetFilename(common, root.InputRoot, file);

        html.BeginJoin($"Inspecting - {display_name}");

        WriteInspectHeaderTable
        (
            common, html, root,
            analytics,
            project.ScannedFiles
                .Where(kvp => kvp.Value.AbsoluteIncludes.Contains(file))
                .Select(kvp => kvp.Key),
            "included_by", $"These include {display_name}",
            it => it.AllIncludedBy.Count
        );

        {
            html.PushString("<div id=\"file\">\n");

            var project_file = project.ScannedFiles[file];
            var analytics_file = analytics.FileToData[file];
            var file_lines = Core.FormatNumber(project_file.NumberOfLines);
            var direct_lines = Core.FormatNumber(project_file.AbsoluteIncludes.Select(f => project.ScannedFiles[f].NumberOfLines).Sum());
            var direct_count = Core.FormatNumber(project_file.AbsoluteIncludes.Count);
            var total_lines = Core.FormatNumber(analytics_file.TotalIncludedLines);
            var total_count = Core.FormatNumber(analytics_file.AllIncludes.Count);

            html.PushString($"<h2>{display_name}</h2>\n");

            html.PushString("<table class=\"summary\">");

            html.PushString("<tr>  <th></th>                 <th>Lines</th>              <th>Files</th>           </tr>\n");
            html.PushString($"<tr>  <th>Lines:</th>           <td class=\"num\">{file_lines}</td>   <td class=\"num\">1</td> </tr>\n");
            html.PushString($"<tr>  <th>Direct Includes:</th> <td class=\"num\">{direct_lines}</td>  <td class=\"num\">{direct_count}</td>  </tr>\n");
            html.PushString($"<tr>  <th>Total Includes:</th>  <td class=\"num\">{total_lines}</td>  <td class=\"num\">{total_count}</td> </tr>\n");

            html.PushString("</table>");

            html.PushString("</div>\n");
        }

        WriteInspectHeaderTable
        (
            common, html, root,
            analytics,
            project.ScannedFiles[file].AbsoluteIncludes,
            "includes", $"{display_name} includes these",
            it => it.AllIncludes.Count
        );

        html.End();

        var filename = Html.GetSafeInspectFilenameHtml(file);
        var path = Path.Join(root.OutputDirectory, filename);
        html.WriteToFile(path);
    }

}


internal static class UiFacade
{
    internal static int HandleNewHero(string project_file, bool overwrite, Log print)
    {
        if (File.Exists(project_file) && overwrite == false)
        {
            print.Error($"{project_file} already exists.");
            return -1;
        }
        var input = new UserInput();
        input.IncludeDirectories.Add("list of relative or absolute directories");
        input.ProjectDirectories.Add("list of relative or absolute source directories (or files)");
        input.PrecompiledHeaders.Add("list of relative pchs, if there are any");

        var content = JsonUtil.Write(input);
        File.WriteAllText(project_file, content);
        return 0;
    }

    internal static int HandleRunHeroHtml(string project_file, string output_directory, Log print)
    {
        var input = UserInput.LoadFromFile(print, project_file);
        if (input == null)
        {
            return -1;
        }
        if (input.Validate(print) == false)
        {
            return -1;
        }
        var input_root = new FileInfo(project_file).DirectoryName ?? Environment.CurrentDirectory;
        input.Decorate(print, input_root);
        Directory.CreateDirectory(output_directory);
        Ui.ScanAndGenerateHtml(print, input, new(input_root, output_directory));
        return 0;
    }

    internal static int RunHeroGraphviz(
        string project_file, string output_file, bool simplify_graphviz, bool only_headers, bool cluster,
        string[] exclude, Log print)
    {
        var input = UserInput.LoadFromFile(print, project_file);
        if (input == null)
        {
            return -1;
        }
        if (input.Validate(print) == false)
        {
            return -1;
        }
        var input_root = new FileInfo(project_file).DirectoryName ?? Environment.CurrentDirectory;
        input.Decorate(print, input_root);
        Ui.ScanAndGenerateDot(print, input, new(input_root, output_file), simplify_graphviz, only_headers, exclude, cluster);
        return 0;
    }
}

