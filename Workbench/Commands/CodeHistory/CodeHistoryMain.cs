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
        public TimeResolution Resolution { get; set; } = TimeResolution.Month;

        [CommandOption("--all-files")]
        public bool AllFiles { get; set; } = false;
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        var cwd = new DirectoryInfo(Environment.CurrentDirectory);

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
                var blamed = await Shared.Git.BlameAsync(file)
                    .SelectAsync(line => line.Author.Time)
                    .ToListAsync();
                return ($"Blaming {file.GetRelative(cwd)}", blamed);
            }))
            .SelectMany(x => x);

        var grouped = blamed_times
            .Order()
            .GroupOnTime(x => x, arg.Resolution, (title, times) => new {Title=title.ToString(arg.Resolution), Count=times.Count})
            .ToImmutableArray();

        // display histogram
        AnsiConsole.Write(new BarChart()
            .Width(60)
            .Label("[green bold underline]File age (lines)[/]")
            .CenterLabel()
            .AddItems(grouped, item => new BarChartItem(
                item.Title, item.Count, Color.Blue)));

        return 0;
    }
}

public static class Main
{
    public static void Configure(IConfigurator config, string name)
    {
        config.AddCommand<PrintCodeHistory>(name).WithDescription("Print a histogram for the age of each line");
    }
}
