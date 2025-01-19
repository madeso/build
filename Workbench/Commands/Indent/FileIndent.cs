using Spectre.Console;
using System.Collections.Immutable;
using Workbench.Shared;
using Workbench.Shared.Extensions;

namespace Workbench.Commands.Indent;


internal class FileIndent
{
    public int Min { get; set; }
    public int Max { get; set; }
    public float MaxLevel { get; internal set; }
    public int MaxLine { get; internal set; }
}



internal class UnhandledExtension
{
    public string Extension { get; }
    public int Count { get; }

    public UnhandledExtension(string extension, int count)
    {
        Extension = extension;
        Count = count;
    }
}

internal class MaxIndentation
{
    public string Type { get; }
    public IndentInformation MaxIdent { get; }

    public MaxIndentation(string type, IndentInformation max_ident)
    {
        Type = type;
        MaxIdent = max_ident;
    }
}

internal class IndentInformation
{
    public Fil File { get; }
    public FileIndent Info { get; }
    public string Group { get; }

    public IndentInformation(Fil file, FileIndent info, string group)
    {
        File = file;
        Info = info;
        Group = group;
    }
}

internal static class IndentFunctions
{
    public static IEnumerable<MaxIndentation> GroupExtensionsWithMaxIndent(Vfs vfs,
        int tab_width, bool enable_javadoc_hack, IEnumerable<Fil> files)
        => files
            .Where(x => FileUtil.ClassifySourceOrNull(x) != null)
            .Select(x => new IndentInformation(file: x, info: CollectIndentInformation(vfs, x, tab_width, enable_javadoc_hack), group: FileUtil.ClassifySourceOrNull(x)!))
            .GroupBy(x => x.Group, (group, items) => new MaxIndentation
            (
                type: group,
                max_ident: items.MaxBy(x => x.Info.MaxLevel)!
            ));

    public static IEnumerable<UnhandledExtension> ListUnhandledExtensions(IEnumerable<Fil> files)
        => files
            .Where(x => FileUtil.ClassifySourceOrNull(x) == null)
            .Select(x => x.Extension)
            .GroupBy(x => x, (key, items) => new UnhandledExtension(extension: key, count: items.Count()))
            .OrderByDescending(x => x.Count);

    public static FileIndent CollectIndentInformation(Vfs vfs, Fil f, int tab_width, bool enable_javadoc_hack)
    {
        var lines = f.ReadAllLines(vfs).ToImmutableArray();
        var is_autogenerated = FileUtil.LooksAutoGenerated(lines);

        if (is_autogenerated)
        {
            return new FileIndent { Min = 0, Max = 0, MaxLevel = 0, MaxLine = -1 };
        }

        var indentations = lines
            .Select(line => IndentationForLine(line, tab_width, enable_javadoc_hack))
            .Zip(Functional.Integers(1))
            .Select(x => new LineIndent { Indentation = x.First, Line = x.Second })
            .ToImmutableArray()
            ;
        var mine = indentations.Where(i => i.Indentation > 0).ToImmutableArray();
        var missing = new LineIndent { Indentation = 0, Line = -1 };
        var min = (mine.Length == 0 ? null : mine.MinBy(x => x.Indentation)) ?? missing;
        var max = (indentations.Length == 0 ? null : indentations.MaxBy(x => x.Indentation)) ?? missing;
        var ml = min.Indentation != 0 ? max.Indentation / (float)min.Indentation : 0.0f;

        return new FileIndent { Min = min.Indentation, Max = max.Indentation, MaxLevel = ml, MaxLine = max.Line };
    }

    private static int IndentationForLine(string line, int tab_width, bool enable_javadoc_hack)
    {
        var indent = 0;
        foreach (var c in line)
        {
            switch (c)
            {
                case ' ':
                    indent += 1;
                    break;
                case '\t':
                    indent += tab_width;
                    break;
                default:
                    {
                        if (enable_javadoc_hack && c == '*')
                        {
                            // assume javadoc comment
                            if (indent % 2 == 1)
                            {
                                return indent - 1;
                            }

                            return indent;
                        }
                        else
                        {
                            return indent;
                        }
                    }
            }
        }

        return indent;
    }

    private static IEnumerable<string> FileReadLines(Vfs vfs, Fil path, bool discard_empty)
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


    private static IEnumerable<int> GetAllIndent(Vfs vfs, Fil path, bool discard_empty)
    {
        return FileReadLines(vfs, path, discard_empty)
                .Select(line => line.Length - line.TrimStart().Length)
            ;
    }


    private static int GetMaxIndent(Vfs vfs, Fil path, bool discard_empty)
    {
        return GetAllIndent(vfs, path, discard_empty)
            .DefaultIfEmpty(-1)
            .Max();
    }

    public static int HandleListIndents(Vfs vfs, Dir cwd, string[] args_files, int each, bool args_show,
        bool args_hist, bool discard_empty)
    {
        var stats = new Dictionary<int, List<Fil>>();
        var found_files = 0;

        foreach (var file in FileUtil.SourcesFromArgs(vfs, cwd, args_files, FileUtil.IsHeaderOrSource))
        {
            found_files += 1;

            var counts = !args_hist
                    ? new[] { GetMaxIndent(vfs, file, discard_empty) }
                    : GetAllIndent(vfs, file, discard_empty)
                        .Order()
                        .Distinct()
                        .ToArray()
                ;

            foreach (var count in counts)
            {
                var index = each <= 1
                        ? count
                        : count - count % each
                    ;

                //todo(Gustav): improve this pattern
                if (stats.TryGetValue(index, out var values))
                {
                    values.Add(file);
                }
                else
                {
                    stats.Add(index, new List<Fil> { file });
                }
            }
        }

        var all_sorted = stats.OrderBy(x => x.Key)
            .Select(x => (
                each <= 1 ? $"{x.Key}" : $"{x.Key}-{x.Key + each - 1}",
                x.Key,
                x.Value
            ))
            .ToImmutableArray();
        AnsiConsole.WriteLine($"Found {found_files} files.");

        if (args_hist)
        {
            var chart = new BarChart()
                .Width(60)
                .Label("[green bold underline]Number of files / indentation[/]")
                .CenterLabel();
            foreach (var (label, _, files) in all_sorted)
            {
                chart.AddItem(label, files.Count, Color.Green);
            }
            AnsiConsole.Write(chart);
        }
        else
        {
            foreach (var (label, _, files) in all_sorted)
            {
                AnsiConsole.WriteLine($"{label}: {(args_show ? files : files.Count)}");
            }
        }

        return 0;
    }
}

internal class LineIndent
{
    public int Indentation { get; set; }
    public int Line { get; set; }
}
