using Spectre.Console.Cli;
using Spectre.Console;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Workbench.Shared;

namespace Workbench.Commands.LineCount;



public static class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddCommand<LineCountCommand>(name);
    }
}

internal record LineCollectedInformation(
    int Count,
    string CountStr,
    List<Fil> Files
);

internal record LineData(
    IEnumerable<LineCollectedInformation> Collected,
    int FileCount
);

[Description("list line counts")]
internal sealed class LineCountCommand : Command<LineCountCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Files/dirs to read")]
        [CommandArgument(0, "<input files/dirs>")]
        public string[] Files { get; set; } = Array.Empty<string>();

        [Description("Display as a historgram instead")]
        [CommandOption("--histogram")]
        [DefaultValue(false)]
        public bool DisplayHistogram { get; set; } = false;

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
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        var cwd = Dir.CurrentDirectory;
        var vfs = new VfsDisk();

        var data = CollectData(cwd, vfs, arg);
        
        AnsiConsole.WriteLine($"Found {data.FileCount} files.");

        if (arg.DisplayHistogram)
        {
            DisplayHistogram(data.FileCount, data.Collected);
        }
        else
        {
            DisplayConsole(data.Collected, arg.Show);
        }

        return 0;
    }

    private static LineData CollectData(Dir cwd, VfsDisk vfs, Arg arg)
    {
        var stats = new Dictionary<int, List<Fil>>();
        var file_count = 0;

        foreach (var file in ListFiles(arg, vfs, cwd, arg.Files))
        {
            file_count += 1;

            var count = ReadAllLines(vfs, file, arg.DiscardEmpty)
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

        var collected = stats.OrderBy(x => x.Key).Select(kvp => new LineCollectedInformation
        (
            Count: kvp.Value.Count,
            CountStr: arg.Each <= 1 ? $"{kvp.Key}" : $"{kvp.Key}-{kvp.Key + arg.Each - 1}",
            Files: kvp.Value
        ));

        return new LineData(collected, file_count);
    }

    private static IEnumerable<Fil> ListFiles(Arg arg, VfsDisk vfs, Dir cwd, string[] files_argument)
    {
        return FileUtil.ListFilesFromArgs(vfs, cwd, files_argument)
            .Where(arg.AllLanguages ? f => FileUtil.ClassifySource(f) != Language.Unknown : FileUtil.IsHeaderOrSource);
    }

    private static void DisplayConsole(IEnumerable<LineCollectedInformation> collected, bool arg_show)
    {
        foreach (var cc in collected)
        {
            if (arg_show && cc.Count < 3)
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

    private static void DisplayHistogram(int file_count, IEnumerable<LineCollectedInformation> collected)
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

    static IEnumerable<string> ReadAllLines(Vfs vfs, Fil path, bool discard_empty)
    {
        var lines = path.ReadAllLines(vfs);

        if (!discard_empty)
        {
            return lines;
        }

        return lines
                .Where(line => string.IsNullOrWhiteSpace(line) == false)
            ;
    }

    static string MonthToString(double m)
    {
        int years = 0;
        if (m > 12)
        {
            years = (int)Math.Floor(m / 12);
            m -= years * 12;
        }

        var months = (int)Math.Ceiling(m);

        var ms = months == 1 ? "1 month" : $"{months} months";
        var ys = years == 1 ? "1 year" : $"{years} years";
        return years > 0 ? $"{ys}, {ms}" : ms;
    }
}


internal enum SoftwareProject
{
    Organic,
    SemiDetached,
    Embedded
}

// https://en.wikipedia.org/wiki/COCOMO
internal record Cocomo(double PesonMonths, double DevelopmentTime, double EffectiveNumberOfPersons)
{
    // eaf = effort adjustment factor = Typical values for EAF range from 0.9 to 1.4. 
    public static Cocomo Calculate(double kloc, SoftwareProject sp, double eaf)
    {
        var (a, b, c) = sp switch
        {
            SoftwareProject.Organic => (3.2, 1.05, 0.38),
            SoftwareProject.SemiDetached => (3.0, 1.12, 0.35),
            SoftwareProject.Embedded => (2.8, 1.20, 0.32),
            _ => throw new Exception("Invalid type")
        };

        var person_months = a * Math.Pow(kloc, b) * eaf;
        var development_time = 2.5 * Math.Pow(person_months, c);
        var effective_number_of_persons = person_months / development_time;
        return new Cocomo(person_months, development_time, effective_number_of_persons);
    }
}