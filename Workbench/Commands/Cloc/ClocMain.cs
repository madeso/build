﻿using System.Collections.Immutable;
using Spectre.Console.Cli;
using Spectre.Console;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Workbench.Shared;

namespace Workbench.Commands.Cloc;

public static class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddCommand<ClocCommand>(name);
    }
}

internal class LangStat
{
    public int Files { get; set; } = 0;
    public int TotalLines { get; set; } = 0;
    public int EmptyLines { get; set; } = 0;
}

internal record LangCollectedInformation(
    string Language,
    int FileCount,
    int EmptyLines,
    int TotalLines
    );

internal record LangData(
    ImmutableArray<LangCollectedInformation> Collected,
    Dictionary<Language, List<Fil>>? Parsed
);

[Description("cloc clone")]
internal sealed class ClocCommand : Command<ClocCommand.Arg>
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

        [Description("Also include unknown sources")]
        [CommandOption("--unknown")]
        [DefaultValue(false)]
        public bool IncludeUnknown { get; set; } = false;

        [Description("Add files for language")]
        [CommandOption("--lang")]
        [DefaultValue(false)]
        public bool AddLanguageFiles { get; set; } = false;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        var cwd = Dir.CurrentDirectory;
        var vfs = new VfsDisk();

        var data = CollectData(cwd, vfs, arg);

        if (arg.DisplayHistogram)
        {
            DisplayHistogram(data.Collected);
        }
        else
        {
            DisplayConsole(data.Collected, data.Parsed, cwd);
        }

        return 0;
    }

    private static LangData CollectData(Dir cwd, VfsDisk vfs, Arg arg)
    {
        var stats = new Dictionary<Language, LangStat>();
        var parsed = arg.AddLanguageFiles ? new Dictionary<Language, List<Fil>>() : null;

        foreach (var file in ListFiles(arg, vfs, cwd, arg.Files))
        {
            var lang = FileUtil.ClassifySource(file);

            if (parsed != null)
            {
                if (parsed.TryGetValue(lang, out var file_list) == false)
                {
                    file_list = new List<Fil>();
                    parsed.Add(lang, file_list);
                }
                file_list.Add(file);
            }

            if (stats.TryGetValue(lang, out var data_values) == false)
            {
                data_values = new LangStat();
                stats.Add(lang, data_values);
            }

            var lines = file.ReadAllLines(vfs).ToImmutableArray();
            data_values.Files += 1;
            data_values.TotalLines += lines.Length;
            data_values.EmptyLines += lines.Count(string.IsNullOrEmpty);
        }

        var collected = stats.OrderBy(x => x.Value.TotalLines).Select(kvp => new LangCollectedInformation
        (
            Language: FileUtil.ToString(kvp.Key),
            FileCount: kvp.Value.Files,
            EmptyLines: kvp.Value.EmptyLines,
            TotalLines: kvp.Value.TotalLines
        )).ToImmutableArray();

        return new LangData(collected, parsed);
    }

    private static IEnumerable<Fil> ListFiles(Arg arg, VfsDisk vfs, Dir cwd, string[] files_argument)
    {
        return FileUtil.ListFilesFromArgs(vfs, cwd, files_argument)
            .Where(f => arg.IncludeUnknown != false || FileUtil.ClassifySource(f) != Language.Unknown);
    }

    private static void DisplayConsole(ImmutableArray<LangCollectedInformation> collected, Dictionary<Language, List<Fil>>? parsed, Dir cwd)
    {
        var t = new Table();
        t.AddColumn("Language");
        t.AddColumn(new TableColumn("Files").RightAligned());
        t.AddColumn(new TableColumn("Empty lines").RightAligned());
        t.AddColumn(new TableColumn("Total lines").RightAligned());

        foreach (var cc in collected)
        {
            t.AddRow(cc.Language,
                cc.FileCount.ToString("N0"),
                cc.EmptyLines.ToString("N0"),
                cc.TotalLines.ToString("N0"));
        }

        t.AddEmptyRow();

        var sum_file_count = collected.Select(cc => cc.FileCount).Sum();
        var sum_empty_lines = collected.Select(cc => cc.EmptyLines).Sum();
        var sum_total_lines = collected.Select(cc => cc.TotalLines).Sum();

        t.AddRow("SUM",
            sum_file_count.ToString("N0"),
            sum_empty_lines.ToString("N0"),
            sum_total_lines.ToString("N0"));

        AnsiConsole.Write(t);

        if (parsed != null)
        {
            foreach (var (lang, list) in parsed)
            {
                var ft = new Table();
                ft.AddColumn(FileUtil.ToString(lang));
                foreach (var f in list)
                {
                    ft.AddRow(f.GetRelative(cwd));
                }
                AnsiConsole.Write(ft);
            }
        }

        var x = Cocomo.Calculate(sum_total_lines / 1000.0, SoftwareProject.Organic, 1.0f);
        AnsiConsole.MarkupLineInterpolated($"COCOMO estimation it took [BLUE]{MonthToString(x.PesonMonths)}[/] to make, a team of [BLUE]{(int)Math.Ceiling(x.EffectiveNumberOfPersons)}[/] could do it in [BLUE]{MonthToString(x.DevelopmentTime)}[/]");
    }

    private static void DisplayHistogram(ImmutableArray<LangCollectedInformation> collected)
    {
        if(collected.Length > 0)
        {
            AnsiConsole.Write(new BarChart()
                .Width(60)
                .Label("[green bold underline]Line counts (files)[/]")
                .CenterLabel()
                .AddItems(collected, item => new BarChartItem(
                    item.Language, item.TotalLines, Color.Blue)));
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
            years = (int) Math.Floor(m / 12);
            m -= years * 12;
        }

        var months = (int) Math.Ceiling(m);

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