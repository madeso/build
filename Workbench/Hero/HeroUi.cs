using Workbench.Utils;

namespace Workbench.Hero;

///////////////////////////////////////////////////////////////////////////////////////////////////
// MainForm


internal static class Ui
{

    public static void scan_and_generate_html(Printer printer, Data.UserInput input, Data.OutputFolders root)
    {
        var project = new Data.Project(input);
        var scanner = new Parser.Scanner();
        var feedback = new Parser.ProgressFeedback(printer);
        scanner.rescan(project, feedback);
        generate_report(root, project, scanner);
    }

    public static void scan_and_generate_dot(Printer printer, Data.UserInput input, Data.OutputFolders root, bool simplifyGraphviz, bool onlyHeaders, string[] exclude, bool cluster)
    {
        var project = new Data.Project(input);
        var scanner = new Parser.Scanner();
        var feedback = new Parser.ProgressFeedback(printer);
        scanner.rescan(project, feedback);
        generate_dot(printer, root, project, scanner, simplifyGraphviz, onlyHeaders, exclude, input, cluster);
    }


    ///////////////////////////////////////////////////////////////////////////////////////////////////
    // ReportForm

    private static bool file_is_in_file_list(string f, IEnumerable<string> list)
    {
        var ff = new FileInfo(f);
        foreach (var p in list)
        {
            var pp = new FileInfo(p);

            // todo(Gustav): improve this match
            if (ff.FullName == pp.FullName)
            {
                return true;
            }
        }
        return false;
    }

    private static bool exclude_file(string file, Data.UserInput input, bool only_headers, IEnumerable<string> exclude)
    {
        if (file_is_in_file_list(file, input.ProjectDirectories))
        {
            // explicit inlcuded... then it's not excluded
            return false;
        }
        else if (only_headers && FileUtil.is_header(file) == false)
        {
            return true;
        }
        else if (file_is_in_file_list(file, exclude))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private static void generate_dot(Printer print, Data.OutputFolders root, Data.Project project, Parser.Scanner scanner, bool simplifyGraphviz, bool onlyHeaders, string[] exclude, Data.UserInput input, bool cluster)
    {
        var analytics = Parser.Analytics.analyze(project);
        var gv = new Graphviz();

        foreach (var file in project.ScannedFiles.Keys)
        {
            if (exclude_file(file, input, onlyHeaders, exclude))
            {
                print.Info($"{file} rejected due to non-header");
                continue;
            }
            else
            {
                print.Info($"{file} added as a node");
            }
            var display_name = Html.get_filename(root.InputRoot, file);
            var node_id = Html.safe_inspect_filename_without_html(file);
            var addedNode = gv.AddNodeWithId(display_name, Shape.box, node_id);
            if (cluster)
            {
                var parent = new FileInfo(file).Directory?.FullName;
                if (parent != null)
                {
                    addedNode.cluster = gv.FindOrCreateCluster(Path.GetRelativePath(root.InputRoot, parent));
                }
            }
        }

        foreach (var file in project.ScannedFiles.Keys)
        {
            if (exclude_file(file, input, onlyHeaders, exclude))
            {
                print.Info($"{file} rejected due to non-header");
                continue;
            }

            var from_file = Html.safe_inspect_filename_without_html(file);
            var from_id = gv.GetNodeFromId(from_file);
            if (from_id == null)
            {
                throw new Exception("BUG: Node not added");
            }

            foreach (var s in project.ScannedFiles[file].AbsoluteIncludes)
            {
                if (exclude_file(s, input, onlyHeaders, exclude))
                {
                    print.Info($"{s} rejected due to non-header");
                    continue;
                }

                var to_file = Html.safe_inspect_filename_without_html(s);
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

        if (simplifyGraphviz)
        {
            gv.Simplify();
        }

        gv.WriteFile(root.OutputDirectory);
    }

    private static void generate_report(Data.OutputFolders root, Data.Project project, Parser.Scanner scanner)
    {
        {
            var html = new Html();
            html.begin("Errors");
            foreach (var s in scanner.errors)
            {
                html.push_str($"<p>{s}</p>");
            }
            html.push_str("<h1>Unhandled extensions</h1>");
            foreach (var (ext, count) in scanner.missing_ext.MostCommon())
            {
                html.push_str($"<p>{ext} {count}</p>");
            }
            html.end();

            var path = Path.Join(root.OutputDirectory, "errors.html");
            html.write_to_file(path);
        }

        {
            var html = new Html();
            html.begin("Missing");
            // todo(Gustav): sort missing on paths or count?
            foreach (var (include_path, origins) in scanner.not_found_origins)
            {
                var count = origins.Count;
                var s = count == 1 ? "" : "s";
                html.push_str($"<div class=\"missing\">{include_path} from <span class=\"num\">{count}</span> file{s} <ul>");

                foreach (var file in origins)
                {
                    html.push_str($"<li>{Html.inspect_filename_link(root.InputRoot, file)}</li>");
                }
                html.push_str("</ul></div>");

            }
            html.end();

            var path = Path.Join(root.OutputDirectory, "missing.html");
            html.write_to_file(path);
        }

        var analytics = Parser.Analytics.analyze(project);
        Html.write_css_file(root.OutputDirectory);
        Parser.Report.GenerateIndexPage(root, project, analytics);

        foreach (var f in project.ScannedFiles.Keys)
        {
            write_inspection_page(root, f, project, analytics);
        }
    }

    private static void write_inspect_header_table
    (
        Html html,
        Data.OutputFolders root,
        Parser.Analytics analytics,
        IEnumerable<string> included,
        string klass,
        string header,
        Func<Parser.ItemAnalytics, int> length_fun
    )
    {
        html.push_str($"<div id=\"{klass}\">\n");
        html.push_str($"<h2>{header}</h2>\n");
        html.push_str("<table class=\"list\">\n");
        html.push_str("<tr>  <th class=\"file\">File</th>  <th>Count</th>  <th>Lines</th>  </tr>\n");

        foreach (var s in included.OrderByDescending(s => length_fun(analytics.file_to_data[s])))
        {
            var display_filename = Html.inspect_filename_link(root.InputRoot, s);
            var display_count = Core.FormatNumber(length_fun(analytics.file_to_data[s]));
            var display_lines = Core.FormatNumber(analytics.file_to_data[s].total_included_lines);

            html.push_str($"<tr><td class=\"file\">{display_filename}</td> <td class=\"num\">{display_count}</td> <td class=\"num\">{display_lines}</td></tr>");
        }

        html.push_str("</table>\n");
        html.push_str("</div>\n");
    }

    private static void write_inspection_page(Data.OutputFolders root, string file, Data.Project project, Parser.Analytics analytics)
    {
        var html = new Html();

        var display_name = Html.get_filename(root.InputRoot, file);

        html.begin($"Inspecting - {display_name}");

        write_inspect_header_table
        (
            html, root,
            analytics,
            project.ScannedFiles
                .Where(kvp => kvp.Value.AbsoluteIncludes.Contains(file))
                .Select(kvp => kvp.Key),
            "included_by", $"Theese include {display_name}",
            it => it.all_included_by.Count
        );

        {
            html.push_str("<div id=\"file\">\n");

            var project_file = project.ScannedFiles[file];
            var analytics_file = analytics.file_to_data[file];
            var file_lines = Core.FormatNumber(project_file.NumberOfLines);
            var direct_lines = Core.FormatNumber(project_file.AbsoluteIncludes.Select(f => project.ScannedFiles[f].NumberOfLines).Sum());
            var direct_count = Core.FormatNumber(project_file.AbsoluteIncludes.Count);
            var total_lines = Core.FormatNumber(analytics_file.total_included_lines);
            var total_count = Core.FormatNumber(analytics_file.all_includes.Count);

            html.push_str($"<h2>{display_name}</h2>\n");

            html.push_str("<table class=\"summary\">");

            html.push_str("<tr>  <th></th>                 <th>Lines</th>              <th>Files</th>           </tr>\n");
            html.push_str($"<tr>  <th>Lines:</th>           <td class=\"num\">{file_lines}</td>   <td class=\"num\">1</td> </tr>\n");
            html.push_str($"<tr>  <th>Direct Includes:</th> <td class=\"num\">{direct_lines}</td>  <td class=\"num\">{direct_count}</td>  </tr>\n");
            html.push_str($"<tr>  <th>Total Includes:</th>  <td class=\"num\">{total_lines}</td>  <td class=\"num\">{total_count}</td> </tr>\n");

            html.push_str("</table>");

            html.push_str("</div>\n");
        }

        write_inspect_header_table
        (
            html, root,
            analytics,
            project.ScannedFiles[file].AbsoluteIncludes,
            "includes", $"{display_name} includes theese",
            it => it.all_includes.Count
        );

        html.end();

        var filename = Html.safe_inspect_filename_html(file);
        var path = Path.Join(root.OutputDirectory, filename);
        html.write_to_file(path);
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
        var inputRoot = new FileInfo(projectFile).DirectoryName ?? Environment.CurrentDirectory;
        input.Decorate(print, inputRoot);
        Directory.CreateDirectory(outputDirectory);
        Ui.scan_and_generate_html(print, input, new(inputRoot, outputDirectory));
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
        var inputRoot = new FileInfo(projectFile).DirectoryName ?? Environment.CurrentDirectory;
        input.Decorate(print, inputRoot);
        Ui.scan_and_generate_dot(print, input, new(inputRoot, outputFile), simplifyGraphviz, onlyHeaders, exclude, cluster);
        return 0;
    }
}

