using Spectre.Console.Cli;
using Spectre.Console;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Workbench.Shared;
using System.Text.RegularExpressions;

namespace Workbench.Commands.LineCount;



public class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddCommand<LineCountCommand>(name);
    }
}



[Description("list line counts")]
internal sealed class LineCountCommand : Command<LineCountCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("File to read")]
        [CommandArgument(0, "<input files>")]
        public string[] Files { get; set; } = Array.Empty<string>();

        [CommandOption("--each")]
        [DefaultValue(1)]
        public int Each { get; set; } = 1;

        [CommandOption("--show")]
        [DefaultValue(false)]
        public bool Show { get; set; } = false;

        [CommandOption("--include-empty")]
        [DefaultValue(true)]
        public bool DiscardEmpty { get; set; } = true;

        [CommandOption("--all-lang")]
        [DefaultValue(false)]
        public bool AllLanguages { get; set; } = false;

        [CommandOption("--histogram")]
        [DefaultValue(false)]
        public bool DisplayHistogram { get; set; } = false;


    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        var stats = new Dictionary<int, List<Fil>>();
        var file_count = 0;

        foreach (var file in FileUtil.SourcesFromArgs(arg.Files, arg.AllLanguages ? f => FileUtil.ClassifySource(f) != Language.Unknown : FileUtil.IsHeaderOrSource))
        {
            file_count += 1;

            var count = file_read_lines(file, arg.DiscardEmpty)
                .Count();

            var index = arg.Each <= 1 ? count : count - count % arg.Each;

            // todo(Gustav): fix this pattern
            if (stats.TryGetValue(index, out var data_values))
            {
                data_values.Add(file);
            }
            else
            {
                stats.Add(index, new List<Fil> { file });
            }
        }

        var collected = stats.OrderBy(x => x.Key).Select(kvp => new
        {
            Count = kvp.Value.Count,
            CountStr = arg.Each <= 1 ? $"{kvp.Key}" : $"{kvp.Key}-{kvp.Key + arg.Each - 1}",
            Files = kvp.Value
        });
        
        AnsiConsole.WriteLine($"Found {file_count} files.");

        if (arg.DisplayHistogram)
        {
            if(file_count > 0)
            {
                AnsiConsole.Write(new BarChart()
                    .Width(60)
                    .Label("[green bold underline]Line counts (files)[/]")
                    .CenterLabel()
                    .AddItems(collected, item => new BarChartItem(
                        item.CountStr, item.Count, Color.Blue)));
            }
        }
        else
        {
            foreach (var cc in collected)
            {
                if (arg.Show && cc.Count < 3)
                {
                    AnsiConsole.WriteLine($"{cc.CountStr}:");
                    foreach (var f in cc.Files)
                    {
                        AnsiConsole.WriteLine($"\t{f}");
                    }
                }
                else
                {
                    AnsiConsole.WriteLine($"{cc.CountStr}:\t{cc.Count}");
                }
            }
        }

        return 0;

        static IEnumerable<string> file_read_lines(Fil path, bool discard_empty)
        {
            var lines = path.ReadAllLines();

            if (!discard_empty)
            {
                return lines;
            }

            return lines
                    .Where(line => string.IsNullOrWhiteSpace(line) == false)
                ;
        }
    }
}