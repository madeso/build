using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;
using Workbench.CMake;

namespace Workbench.Commands.Cmake;


internal sealed class TraceCommand : Command<TraceCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("File to read")]
        [CommandArgument(0, "<input file>")]
        public string File { get; set; } = "";
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(printer => {
            var cmake = CmakeTools.FindInstallationOrNull(printer);

            if (cmake == null)
            {
                printer.Error("Failed to find cmake");
                return -1;
            }

            AnsiConsole.MarkupLineInterpolated($"Loading [green]{settings.File}[/].");

            try
            {
                var lines = Trace.TraceDirectory(cmake, settings.File);
                foreach (var li in lines)
                {
                    AnsiConsole.MarkupLineInterpolated($"Running [green]{li.Cmd}[/].");
                }
            }
            catch (TraceError x)
            {
                AnsiConsole.MarkupLineInterpolated($"Error: [red]{x.Message}[/]");
            }

            return 0;
        });
    }
}

internal sealed class DotCommand : Command<DotCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("File to read")]
        [CommandArgument(0, "<input file>")]
        public string File { get; set; } = "";

        [Description("Simplify dotfile output")]
        [CommandOption("--simplify")]
        [DefaultValue(false)]
        public bool Simplify { get; set; }

        [Description("Remove empty projects from dotfile")]
        [CommandOption("--remove-empty")]
        [DefaultValue(false)]
        public bool RemoveEmpty { get; set; }

        [Description("Remove interface libraries")]
        [CommandOption("--remove-interfaces")]
        [DefaultValue(false)]
        public bool RemoveInterface { get; set; }

        [Description("Reverse arrows")]
        [CommandOption("--reverse")]
        [DefaultValue(false)]
        public bool Reverse { get; set; }

        [Description("Where to write dot")]
        [CommandArgument(1, "[output file]")]
        [DefaultValue("output.dot")]
        public string Output { get; set; } = "";

        [Description("Project names to ignore")]
        [CommandOption("--ignores")]
        public string[] NamesToIgnore { get; set; } = Array.Empty<string>(); 
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(printer => {
            var cmake = CmakeTools.FindInstallationOrNull(printer);

            if(cmake == null)
            {
                printer.Error("Failed to find cmake");
                return -1;
            }

            AnsiConsole.MarkupLineInterpolated($"Loading [green]{settings.File}[/].");

            try
            {
                var ignores = settings.NamesToIgnore.ToImmutableHashSet();
                AnsiConsole.MarkupLineInterpolated($"Ignoring [red]{ignores.Count}[/] projects.");
                var lines = Trace.TraceDirectory(cmake, settings.File);
                var solution = Solution.Parse.CMake(lines);

                if (settings.RemoveInterface)
                {
                    var removed = solution.RemoveProjects(p => p.Type == Solution.ProjectType.Interface);
                    AnsiConsole.MarkupLineInterpolated($"Removing [red]{removed}[/] interfaces.");
                }

                solution.RemoveProjects(p => ignores.Contains(p.Name));
                if(settings.RemoveEmpty)
                {
                    solution.RemoveProjects(p => p.Uses.Any() == false && p.IsUsedBy.Any() == false);
                }

                var gv = solution.MakeGraphviz(settings.Reverse);

                if (settings.Simplify)
                {
                    gv.Simplify();
                }

                var output = gv.Lines.ToArray();

                if (settings.Output.ToLower() == "stdout")
                {
                    foreach (var l in output)
                    {
                        AnsiConsole.WriteLine(l);
                    }
                }
                else
                {
                    AnsiConsole.MarkupLineInterpolated($"Writing {output.Length} lines of dot to {settings.Output}");
                    File.WriteAllLines(settings.Output, output);
                }
            }
            catch (TraceError x)
            {
                AnsiConsole.MarkupLineInterpolated($"Error: [red]{x.Message}[/]");
            }

            return 0;
        });
    }
}

internal class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, cmake =>
        {
            cmake.AddCommand<TraceCommand>("trace");
            cmake.AddCommand<DotCommand>("dot");
        });
    }
}