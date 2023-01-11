using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Workbench.CMake;


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
        AnsiConsole.MarkupLine($"Loading [green]{settings.File}[/].");

        try
        {
            var lines = Trace.TraceDirectory(settings.File);
            foreach (var li in lines)
            {
                AnsiConsole.MarkupLine($"Running [green]{li.Cmd}[/].");
            }
        }
        catch (TraceError x)
        {
            AnsiConsole.MarkupLine($"Error: [red]{x.Message}[/]");
        }

        return 0;
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
        public string[]? NamesToIgnore { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        AnsiConsole.MarkupLine($"Loading [green]{settings.File}[/].");

        try
        {
            var ignores = settings.NamesToIgnore ?? Array.Empty<string>();
            AnsiConsole.MarkupLine($"Ignoring [red]{ignores.Length}[/] projects.");
            var lines = Trace.TraceDirectory(settings.File);
            var solution = Solution.Parse(lines);

            if (settings.RemoveInterface)
            {
                var interaces = solution.JustProjects.Where(p => p.Type == Solution.ProjectType.Interface).Select(p => p.Name).ToArray();
                AnsiConsole.MarkupLine($"Removing [red]{interaces.Length}[/] interfaces.");
                solution.RemoveProjects(interaces);
            }

            if (settings.Simplify)
            {
                solution.Simplify();
            }

            var gv = solution.MakeGraphviz(settings.Reverse, settings.RemoveEmpty, ignores);

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
                AnsiConsole.MarkupLine($"Writing {output.Length} lines of dot to {settings.Output}");
                File.WriteAllLines(settings.Output, output);
            }
        }
        catch (TraceError x)
        {
            AnsiConsole.MarkupLine($"Error: [red]{x.Message}[/]");
        }

        return 0;
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
