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

        private static IEnumerable<string> Exclude(IEnumerable<string> argsExclude, bool cmake)
        {
            if (cmake)
            {
                return argsExclude.Concat(new string[] {
                        "ZERO_CHECK", "RUN_TESTS", "NightlyMemoryCheck", "ALL_BUILD",
                        "Continuous", "Experimental", "Nightly",
                    });
            }
            else
            {
                return argsExclude;
            }
        }

        internal bool ShouldExclude(string displayName)
        {
            var name = Transform(displayName);
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

    private const string GraphvizExtensionNoDot = "gv";

    // ======================================================================================================================
    // logic
    // ======================================================================================================================

    private static void RunGraphviz(Printer printer, string targetFile, string imageFormat, string graphvizLayout)
    {
        var cmdline = new ProcessBuilder(
            "dot",
            targetFile + ".graphviz", "-T" + imageFormat,
            "-K" + graphvizLayout,
            "-O" + targetFile + "." + imageFormat
        );
        printer.Info($"Running graphviz {cmdline}");
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

    public static int handle_generate(Printer printer, string argsTarget,
            string argsFormat,
            ExclusionList exclude,
            bool simplify,
            bool reverseArrows,
            string pathToSolutionFile,
            string argsStyle)
    {
        var solution = SolutionParser.ParseVisualStudio(printer, pathToSolutionFile);

        solution.RemoveProjects(p => exclude.ShouldExclude(p.Name));

        var gv = solution.MakeGraphviz(reverseArrows);

        if (simplify)
        {
            gv.Simplify();
        }

        var imageFormat = GetValueOrDefault(argsFormat, "svg");
        var graphvizLayout = GetValueOrDefault(argsStyle, "dot");
        var targetFile = GetValueOrDefault(argsTarget, ChangeExtension(pathToSolutionFile, GraphvizExtensionNoDot));

        gv.WriteFile(targetFile);

        RunGraphviz(printer, targetFile, imageFormat, graphvizLayout);

        return 0;
    }

    private static string ChangeExtension(string file, string newExtension)
    {
        var dir = new FileInfo(file).Directory?.FullName!;
        var name = Path.GetFileNameWithoutExtension(file);
        return Path.Join(dir, $"{name}.{newExtension}");
    }

    public static int SourceCommand(Printer printer, ExclusionList exclude,
            bool simplify,
            bool reverseArrows,
            string pathToSolutionFile)
    {
        var solution = SolutionParser.ParseVisualStudio(printer, pathToSolutionFile);

        solution.RemoveProjects(p => exclude.ShouldExclude(p.Name));

        var gv = solution.MakeGraphviz(reverseArrows);

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

    public static int WriteCommand(Printer printer, ExclusionList exclude, string targetFileOrEmpty,
            bool simplify,
            bool reverseArrows,
            string pathToSolutionFile)
    {
        var solution = SolutionParser.ParseVisualStudio(printer, pathToSolutionFile);

        solution.RemoveProjects(p => exclude.ShouldExclude(p.Name));

        var gv = solution.MakeGraphviz(reverseArrows);

        if (simplify)
        {
            gv.Simplify();
        }

        var targetFile = GetValueOrDefault(targetFileOrEmpty, ChangeExtension(pathToSolutionFile, GraphvizExtensionNoDot));

        gv.WriteFile(targetFile);

        printer.Info($"Wrote {targetFile}");

        return 0;
    }

    public static int ListCommand(Printer printer, string solutionPath)
    {
        var solution = SolutionParser.ParseVisualStudio(printer, solutionPath);
        foreach (var project in solution.Projects)
        {
            printer.Info(project.Name);
        }

        printer.Info("");

        return 0;
    }
}
