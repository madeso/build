﻿using System.Collections.Immutable;
using Spectre.Console.Cli;
using Spectre.Console;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Workbench.Shared;
using System.Text.RegularExpressions;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace Workbench.Commands.Cloc;

public class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddCommand<ClocCommand>(name);
    }
}

class Stat
{
    public int Files { get; set; } = 0;
    public int TotalLines { get; set; } = 0;
    public int EmptyLines { get; set; } = 0;
}

[Description("cloc clone")]
internal sealed class ClocCommand : Command<ClocCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Files/dirs to read")]
        [CommandArgument(0, "<input files/dirs>")]
        public string[] Files { get; set; } = Array.Empty<string>();

        [Description("Also include unknown sources")]
        [CommandOption("--unknown")]
        [DefaultValue(false)]
        public bool IncludeUnknown { get; set; } = false;

        [Description("Display as a historgram instead")]
        [CommandOption("--histogram")]
        [DefaultValue(false)]
        public bool DisplayHistogram { get; set; } = false;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        var stats = new Dictionary<Language, Stat>();

        foreach (var file in FileUtil.ListFilesFromArgs(arg.Files))
        {
            var lang = FileUtil.ClassifySource(file);

            if (arg.IncludeUnknown == false && lang == Language.Unknown)
            {
                continue;
            }
            
            if (stats.TryGetValue(lang, out var data_values) == false)
            {
                data_values = new Stat();
                stats.Add(lang, data_values);
            }

            var lines = file.ReadAllLines().ToImmutableArray();
            data_values.Files += 1;
            data_values.TotalLines += lines.Length;
            data_values.EmptyLines += lines.Count(string.IsNullOrEmpty);
        }

        var collected = stats.OrderBy(x => x.Value.TotalLines).Select(kvp => new
        {
            Language = FileUtil.ToString(kvp.Key),
            FileCount = kvp.Value.Files,
            EmptyLines = kvp.Value.EmptyLines,
            TotalLines = kvp.Value.TotalLines
        }).ToImmutableArray();

        if (arg.DisplayHistogram)
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
        else
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

            t.AddRow("SUM",
                collected.Select(cc => cc.FileCount).Sum().ToString("N0"),
                collected.Select(cc => cc.EmptyLines).Sum().ToString("N0"),
                collected.Select(cc => cc.TotalLines).Sum().ToString("N0"));

            AnsiConsole.Write(t);
        }

        return 0;
    }
}