using Spectre.Console;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using System.Xml;
using Workbench.Utils;

namespace Workbench.SlnDeps;


static class F
{

    // ======================================================================================================================
    // project
    // ======================================================================================================================

    public class ExclusionList
    {
        readonly ImmutableHashSet<string> explicits;
        readonly ImmutableArray<string> contains;

        public ExclusionList(IEnumerable<string> exclude, IEnumerable<string> contains, bool cmake)
        {
            this.explicits = Exclude(exclude, cmake).Select(Transform).ToImmutableHashSet();
            this.contains = contains.ToImmutableArray();
        }

        private static string Transform(string name)
        {
            return name.ToLowerInvariant().Trim();
        }

        private static IEnumerable<string> Exclude(IEnumerable<string> exclude, bool cmake)
        {
            if (cmake)
            {
                return exclude.Concat(new string[] {
                        "ZERO_CHECK", "RUN_TESTS", "NightlyMemoryCheck", "ALL_BUILD",
                        "Continuous", "Experimental", "Nightly",
                    });
            }
            else
            {
                return exclude;
            }
        }

        internal bool ShouldExclude(string display_name)
        {
            var name = Transform(display_name);
            if (explicits.Contains(name))
            {
                return true;
            }

            if (contains.Any(n => name.Contains(n)))
            {
                return true;
            }

            return false;
        }
    }

    private const string GRAPHVIZ_EXTENSION_NO_DOT = "gv";

    // ======================================================================================================================
    // logic
    // ======================================================================================================================

    private static void RunGraphviz(Printer printer, string target_file, string image_format, string graphviz_layout)
    {
        var cmdline = new ProcessBuilder(
            "dot",
            target_file + ".graphviz", "-T" + image_format,
            "-K" + graphviz_layout,
            "-O" + target_file + "." + image_format
        );
        Printer.Info($"Running graphviz {cmdline}");
        cmdline.RunAndPrintOutput(printer);
    }


    private static string GetValueOrDefault(string value, string def)
    {
        var vt = value.Trim();
        return vt.Trim() == "" || vt.Trim() == "?" ? def : value;
    }


    // ======================================================================================================================
    // Handlers
    // ======================================================================================================================

    public static int handle_generate(Printer printer, string target,
            string format,
            ExclusionList exclude,
            bool simplify,
            bool reverse_arrows,
            string path_to_solution_file,
            string layout_name)
    {
        var solution = SolutionParser.ParseVisualStudio(printer, path_to_solution_file);

        solution.RemoveProjects(p => exclude.ShouldExclude(p.Name));

        var gv = solution.MakeGraphviz(reverse_arrows);

        if (simplify)
        {
            gv.Simplify();
        }

        var image_format = GetValueOrDefault(format, "svg");
        var graphviz_layout = GetValueOrDefault(layout_name, "dot");
        var target_file = GetValueOrDefault(target, ChangeExtension(path_to_solution_file, GRAPHVIZ_EXTENSION_NO_DOT));

        gv.WriteFile(target_file);

        RunGraphviz(printer, target_file, image_format, graphviz_layout);

        return 0;
    }

    private static string ChangeExtension(string file, string new_extension)
    {
        var dir = new FileInfo(file).Directory?.FullName!;
        var name = Path.GetFileNameWithoutExtension(file);
        return Path.Join(dir, $"{name}.{new_extension}");
    }

    public static int SourceCommand(Printer printer, ExclusionList exclude,
            bool simplify,
            bool reverse_arrows,
            string path_to_solution_file)
    {
        var solution = SolutionParser.ParseVisualStudio(printer, path_to_solution_file);

        solution.RemoveProjects(p => exclude.ShouldExclude(p.Name));

        var gv = solution.MakeGraphviz(reverse_arrows);

        if (simplify)
        {
            gv.Simplify();
        }

        foreach (var line in gv.Lines)
        {
            Printer.Info(line);
        }

        return 0;
    }

    public static int WriteCommand(
        Printer printer, ExclusionList exclude, string target_file_or_empty, bool simplify,
            bool reverse_arrows, string path_to_solution_file)
    {
        var solution = SolutionParser.ParseVisualStudio(printer, path_to_solution_file);

        solution.RemoveProjects(p => exclude.ShouldExclude(p.Name));

        var gv = solution.MakeGraphviz(reverse_arrows);

        if (simplify)
        {
            gv.Simplify();
        }

        var target_file = GetValueOrDefault(target_file_or_empty, ChangeExtension(path_to_solution_file, GRAPHVIZ_EXTENSION_NO_DOT));

        gv.WriteFile(target_file);

        Printer.Info($"Wrote {target_file}");

        return 0;
    }

    public static int ListCommand(Printer printer, string solution_path)
    {
        var solution = SolutionParser.ParseVisualStudio(printer, solution_path);
        foreach (var project in solution.Projects)
        {
            Printer.Info(project.Name);
        }

        Printer.Info("");

        return 0;
    }
}
