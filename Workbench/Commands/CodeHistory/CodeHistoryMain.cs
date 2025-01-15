using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Spectre.Console;
using Spectre.Console.Cli;
using Workbench.Config;
using Workbench.Shared;
using Workbench.Shared.Extensions;

namespace Workbench.Commands.CodeHistory;



internal sealed class PrintCodeHistory : AsyncCommand<PrintCodeHistory.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [CommandArgument(0, "<Resolution>")]
        [Description("The resolution to group on")]
        public TimeResolution Resolution { get; set; } = TimeResolution.Month;

        [CommandOption("--all-files")]
        [Description("Look at all files and not just source files")]
        public bool AllFiles { get; set; } = false;

        [CommandOption("--show-files")]
        [Description("Show the files that include the age count")]
        public bool ShowFiles { get; set; } = false;
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        var cwd = Dir.CurrentDirectory;
        return await CliUtil.PrintErrorsAtExitAsync(async log =>
        {

            var git_path = Config.Paths.GetGitExecutable(cwd, log);
            if (git_path == null)
            {
                return -1;
            }

            // get files
            var files = FileUtil.IterateFiles(cwd, false, true);
            if (arg.AllFiles == false)
            {
                files = files.Where(file => FileUtil.ClassifySource(file) != Language.Unknown);
            }

            // get time for all lines in the files
            var blamed_times = (await SpectreExtensions.Progress()
                    .MapArrayAsync(files.ToImmutableArray(), async file =>
                    {
                        var blamed = await Shared.Git.BlameAsync(cwd, git_path, file)
                            .SelectAsync(line => new { File = file, line.Author.Time })
                            .ToListAsync();
                        return ($"Blaming {file.GetRelative(cwd)}", blamed);
                    }))
                .SelectMany(x => x);

            var grouped = blamed_times
                .OrderBy(x => x.Time)
                .GroupOnTime(x => x.Time, arg.Resolution, (title, times) => new
                {
                    Title = title.ToString(arg.Resolution),
                    Files = times.Select(x => x.File).Distinct().ToImmutableArray(),
                    times.Count
                })
                .ToImmutableArray();

            // display histogram
            AnsiConsole.Write(new BarChart()
                .Width(60)
                .Label("[green bold underline]File age (lines)[/]")
                .CenterLabel()
                .AddItems(grouped, item => new BarChartItem(
                    item.Title, item.Count, Color.Blue)));

            if (!arg.ShowFiles)
            {
                return 0;
            }

            foreach (var e in grouped)
            {
                Printer.Header($"{e.Title} ({e.Count})");
                foreach (var f in e.Files)
                {
                    AnsiConsole.WriteLine(f.GetRelative(cwd));
                }

                AnsiConsole.WriteLine();
            }

            return 0;
        });
    }
}

public static class Main
{
    public static void Configure(IConfigurator config, string name)
    {
        config.AddCommand<PrintCodeHistory>(name).WithDescription("Print a histogram for the age of each line");
    }
}
