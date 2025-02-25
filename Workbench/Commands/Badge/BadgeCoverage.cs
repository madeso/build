using System.Text.Json.Serialization;
using Workbench.Commands.Clang;
using Workbench.Config;
using Workbench.Shared;
using static Workbench.Commands.Indent.IndentationCommand;

namespace Workbench.Commands.Badge;

public class CoverageSummaryJson
{
    [JsonPropertyName("line_total")] public int LineTotal {get; set;}
    [JsonPropertyName("line_covered")] public int LineCovered {get; set;}
    [JsonPropertyName("line_percent")] public float LinePercent {get; set;}
    [JsonPropertyName("function_total")] public int FunctionTotal {get; set;}
    [JsonPropertyName("function_covered")] public int FunctionCovered {get; set;}
    [JsonPropertyName("function_percent")] public float FunctionPercent {get; set;}
    [JsonPropertyName("branch_total")] public int BranchTotal {get; set;}
    [JsonPropertyName("branch_covered")] public int BranchCovered {get; set;}
    [JsonPropertyName("branch_percent")] public float BranchPercent {get; set;}
}


internal static class BadgeCoverage
{
    private record ValueSide(BadgeColor Color, string Value);
    private record Collected(int Covered, int Total, float Percentage);

    public static int Convert(Log print, Vfs vfs, Fil? summary_json, Dir output)
    {
        var summary = summary_json != null ? ConfigFile.LoadOrNull<CoverageSummaryJson>(vfs, print, summary_json) : null;

        Wrtie(print, vfs, output, "Line", summary, s => new (s.LineCovered, s.LineTotal, s.LinePercent));
        Wrtie(print, vfs, output, "Function", summary, s => new (s.FunctionCovered, s.FunctionTotal, s.FunctionPercent));
        Wrtie(print, vfs, output, "Branch", summary, s => new (s.BranchCovered, s.BranchTotal, s.BranchPercent));

        return 0;
    }

    private static void Wrtie(Log print, Vfs vfs, Dir output, string name, CoverageSummaryJson? sum, Func<CoverageSummaryJson, Collected> data_converter)
    {
        var data = sum == null ? null : data_converter(sum);
        BadgeGen(print, vfs, output, name, name, data, s => new(ColorFromPercentage(s.Percentage), $"{s.Covered}/{s.Total}"));
        BadgeGen(print, vfs, output, name + "_percentage", name, data, s => new(ColorFromPercentage(s.Percentage), $"{s.Percentage}%"));
    }

    private static BadgeColor ColorFromPercentage(float pc)
    {
        // todo(Gustav): figure out better colors
        if (pc < 20) return BadgeColor.Red;
        if (pc < 60) return BadgeColor.Yellow;
        if (pc < 80) return BadgeColor.LightGreen;
        return BadgeColor.Green;
    }

    private static void BadgeGen(Log print, Vfs vfs, Dir output, string name, string display, Collected? sum, Func<Collected, ValueSide> to_res)
    {
        var file = output.GetFile($"{name.ToLowerInvariant()}.svg");

        var dis = sum == null ? new ValueSide(BadgeColor.LightGrey, "???") : to_res(sum);

        var b = new Shared.Badge()
        {
            Label = display,
            Value = dis.Value,
            ValueColor = dis.Color,
            FontSize = 10,
            CornerRadius = 3f,
            HorizontalPadding = 3,
            StretchText = false
        };

        var svg = b.GenerateSvg();
        vfs.WriteAllText(file, svg);

        print.Info($"Saved file {file}");
    }
}