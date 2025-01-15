using Spectre.Console;
using System.Collections.Immutable;
using Workbench.Shared;

namespace Workbench.Commands.SlnDeps;


static class SlnDepsFunctions
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
            explicits = Exclude(exclude, cmake).Select(Transform).ToImmutableHashSet();
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
                return exclude.Concat(new[] {
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

    private const string GRAPHVIZ_EXTENSION_LEADING_DOT = ".gv";

    // ======================================================================================================================
    // logic
    // ======================================================================================================================

    private static async Task<bool> RunGraphvizAsync(Config.Paths paths, Dir cwd, Log log, Fil target_file, string image_format, string graphviz_layout)
    {
        var dot = paths.GetGraphvizExecutable(cwd, log);
        if (dot == null)
        {
            return false;
        }

        var cmdline = new ProcessBuilder(
            dot,
            target_file + ".graphviz", "-T" + image_format,
            "-K" + graphviz_layout,
            "-O" + target_file + "." + image_format
        );
        AnsiConsole.WriteLine($"Running graphviz {cmdline}");
        await cmdline.RunAndPrintOutputAsync(cwd, log);
        return true;
    }


    // ======================================================================================================================
    // Handlers
    // ======================================================================================================================

    public static async Task<int> HandleGenerateAsync(Config.Paths paths, Dir cwd, Log log, string target,
            string format,
            ExclusionList exclude,
            bool simplify,
            bool reverse_arrows,
            Fil path_to_solution_file,
            string layout_name)
    {
        var solution = Solution.Parse.VisualStudio(log, path_to_solution_file);

        solution.RemoveProjects(p => exclude.ShouldExclude(p.Name));

        var gv = solution.MakeGraphviz(reverse_arrows);

        if (simplify)
        {
            gv.Simplify();
        }

        var image_format = Cli.GetValueOrDefault(format, "svg");
        var graphviz_layout = Cli.GetValueOrDefault(layout_name, "dot");
        var target_file = Cli.GetValueOrDefault(target, path_to_solution_file.ChangeExtension(GRAPHVIZ_EXTENSION_LEADING_DOT));

        await gv.WriteFileAsync(target_file);

        var res = await RunGraphvizAsync(paths, cwd, log, target_file, image_format, graphviz_layout);
        if (res == false)
        {
            return -1;
        }

        return 0;
    }

    public static int SourceCommand(Log log, ExclusionList exclude,
            bool simplify,
            bool reverse_arrows,
            Fil path_to_solution_file)
    {
        var solution = Solution.Parse.VisualStudio(log, path_to_solution_file);

        solution.RemoveProjects(p => exclude.ShouldExclude(p.Name));

        var gv = solution.MakeGraphviz(reverse_arrows);

        if (simplify)
        {
            gv.Simplify();
        }

        foreach (var line in gv.Lines)
        {
            AnsiConsole.WriteLine(line);
        }

        return 0;
    }

    public static int WriteCommand(
        Log log, ExclusionList exclude, string target_file_or_empty, bool simplify,
            bool reverse_arrows, Fil path_to_solution_file)
    {
        var solution = Solution.Parse.VisualStudio(log, path_to_solution_file);

        solution.RemoveProjects(p => exclude.ShouldExclude(p.Name));

        var gv = solution.MakeGraphviz(reverse_arrows);

        if (simplify)
        {
            gv.Simplify();
        }

        var target_file = Cli.GetValueOrDefault(target_file_or_empty, path_to_solution_file.ChangeExtension(GRAPHVIZ_EXTENSION_LEADING_DOT));

        gv.WriteFile(target_file);

        AnsiConsole.WriteLine($"Wrote {target_file}");

        return 0;
    }

    public static int ListCommand(Log log, Fil solution_path)
    {
        var solution = Solution.Parse.VisualStudio(log, solution_path);
        foreach (var project in solution.Projects)
        {
            AnsiConsole.WriteLine(project.Name);
        }

        AnsiConsole.WriteLine("");

        return 0;
    }
}
