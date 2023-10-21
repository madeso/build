using Spectre.Console.Cli;
using Spectre.Console;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Workbench.Shared;

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
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        var stats = new Dictionary<int, List<string>>();
        var file_count = 0;

        foreach (var file in FileUtil.SourcesFromArgs(arg.Files, FileUtil.IsHeaderOrSource))
        {
            file_count += 1;

            var count = file_read_lines(file, arg.DiscardEmpty)
                .Count();

            var index = arg.Each <= 1 ? count : count - count % arg.Each;
            if (stats.TryGetValue(index, out var data_values))
            {
                data_values.Add(file);
            }
            else
            {
                stats.Add(index, new List<string> { file });
            }
        }

        AnsiConsole.WriteLine($"Found {file_count} files.");
        foreach (var (count, files) in stats.OrderBy(x => x.Key))
        {
            var c = files.Count;
            var count_str = arg.Each <= 1 ? $"{count}" : $"{count}-{count + arg.Each - 1}";
            if (arg.Show && c < 3)
            {
                AnsiConsole.WriteLine($"{count_str}: {files}");
            }
            else
            {
                AnsiConsole.WriteLine($"{count_str}: {c}");
            }
        }

        return 0;

        static IEnumerable<string> file_read_lines(string path, bool discard_empty)
        {
            var lines = File.ReadLines(path);

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