using Spectre.Console;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using System.Xml;

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
            this.explicits = Exclude(exclude, cmake).Select(name => Transform(name)).ToImmutableHashSet();
            this.contains = contains.ToImmutableArray();
        }

        private static string Transform(string name)
        {
            return name.ToLowerInvariant().Trim();
        }

        private static IEnumerable<string> Exclude(IEnumerable<string> args_exclude, bool cmake)
        {
            if (cmake)
            {
                return args_exclude.Concat(new string[] {
                        "ZERO_CHECK", "RUN_TESTS", "NightlyMemoryCheck", "ALL_BUILD",
                        "Continuous", "Experimental", "Nightly",
                    });
            }
            else
            {
                return args_exclude;
            }
        }

        internal bool ShouldExclude(string display_name)
        {
            var name = Transform(display_name);
            if (explicits.Contains(name))
            {
                return true;
            }

            if (contains.Where(n=> name.Contains(n)).Any())
            {
                return true;
            }

            return false;
        }
    }

    private static string GRAPHVIZ_EXTENSION_NO_DOT = "gv";

    // ======================================================================================================================
    // logic
    // ======================================================================================================================

    private static void run_graphviz(Printer printer, string target_file, string image_format, string graphviz_layout)
    {
        var cmdline = new ProcessBuilder(
            "dot",
            target_file + ".graphviz", "-T" + image_format,
            "-K" + graphviz_layout,
            "-O" + target_file + "." + image_format
        );
        printer.Info($"Running graphviz {cmdline}");
        cmdline.RunAndPrintOutput(printer);
    }


    private static string value_or_default(string value, string def)
    {
        var vt = value.Trim();
        return vt.Trim() == "" || vt.Trim() == "?" ? def : value;
    }


    // ======================================================================================================================
    // Handlers
    // ======================================================================================================================

    public static int handle_generate(Printer printer, string args_target,
            string args_format,
            ExclusionList exl,
            bool args_simplify,
            bool args_reverse,
            string args_solution,
            string args_style)
    {
        var path_to_solution_file = args_solution;
        var exclude = exl;
        var simplify = args_simplify;
        var reverse_arrows = args_reverse;
        var graphviz_layout = args_style ?? "dot";

        var solution = SolutionParser.ParseVisualStudio(printer, path_to_solution_file);

        solution.RemoveProjects(p => exclude.ShouldExclude(p.Name));

        var gv = solution.MakeGraphviz(reverse_arrows);

        if (simplify)
        {
            gv.Simplify();
        }

        var image_format = value_or_default(args_format, "svg");
        var target_file = value_or_default(args_target, ChangeExtension(path_to_solution_file, GRAPHVIZ_EXTENSION_NO_DOT));

        gv.WriteFile(target_file);

        run_graphviz(printer, target_file, image_format, graphviz_layout);

        return 0;
    }

    private static string ChangeExtension(string file, string new_ext)
    {
        var dir = new FileInfo(file).Directory?.FullName!;
        var name = Path.GetFileNameWithoutExtension(file);
        return Path.Join(dir, $"{name}.{new_ext}");
    }

    public static int handle_source(Printer printer, ExclusionList exclude,
            bool args_simplify,
            bool args_reverse,
            string args_solution)
    {
        var path_to_solution_file = args_solution;
        var simplify = args_simplify;
        var reverse_arrows = args_reverse;

        var solution = SolutionParser.ParseVisualStudio(printer, path_to_solution_file);

        solution.RemoveProjects(p => exclude.ShouldExclude(p.Name));

        var gv = solution.MakeGraphviz(reverse_arrows);

        if (simplify)
        {
            gv.Simplify();
        }

        foreach (var line in gv.Lines)
        {
            printer.Info(line);
        }

        return 0;
    }

    public static int handle_write(Printer printer, ExclusionList exl, string args_target,
            bool args_simplify,
            bool args_reverse,
            string args_solution)
    {
        var path_to_solution_file = args_solution;
        var exclude = exl;
        var simplify = args_simplify;
        var reverse_arrows = args_reverse;

        var solution = SolutionParser.ParseVisualStudio(printer, path_to_solution_file);

        var removed = solution.RemoveProjects(p => exclude.ShouldExclude(p.Name));

        var gv = solution.MakeGraphviz(reverse_arrows);

        if (simplify)
        {
            gv.Simplify();
        }

        var target_file = value_or_default(args_target, ChangeExtension(path_to_solution_file, GRAPHVIZ_EXTENSION_NO_DOT));

        gv.WriteFile(target_file);

        printer.Info($"Wrote {target_file}");

        return 0;
    }

    public static int handle_list(Printer printer, string args_solution)
    {
        var solution = SolutionParser.ParseVisualStudio(printer, args_solution);
        foreach (var project in solution.Projects)
        {
            printer.Info(project.Name);
        }

        printer.Info("");

        return 0;
    }
}