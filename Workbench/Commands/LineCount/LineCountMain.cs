using Spectre.Console.Cli;
using Spectre.Console;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Workbench.Utils;

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

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return Printer.PrintErrorsAtExit(print => HandleLineCountCommand(settings.Files,
            settings.Each, settings.Show, settings.DiscardEmpty));
    }

    public static int HandleLineCountCommand(string[] args_files,
        int each,
        bool args_show,
        bool args_discard_empty)
    {
        var stats = new Dictionary<int, List<string>>();
        var file_count = 0;

        foreach (var file in FileUtil.ListFilesRecursively(args_files, FileUtil.HeaderAndSourceFiles))
        {
            file_count += 1;

            var count = file_read_lines(file, args_discard_empty)
                .Count();

            var index = each <= 1 ? count : count - count % each;
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
            var count_str = each <= 1 ? $"{count}" : $"{count}-{count + each - 1}";
            if (args_show && c < 3)
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