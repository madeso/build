using Spectre.Console;
using System.Collections.Immutable;
using Workbench.Utils;

namespace Workbench.Indent;


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
    public FileInfo File { get; }
    public FileIndent Info { get; }
    public string Group { get; }

    public IndentInformation(FileInfo file, FileIndent info, string group)
    {
        File = file;
        Info = info;
        Group = group;
    }
}

internal static class F
{
    public static IEnumerable<MaxIndentation> GroupExtensionsWithMaxIndent(
        int tab_width, bool enable_javadoc_hack, IEnumerable<FileInfo> files)
        => files
            .Where(x => FileUtil.ClassifySourceOrNull(x) != null)
            .Select(x => new IndentInformation(file: x, info: F.CollectIndentInformation(x, tab_width, enable_javadoc_hack), group: FileUtil.ClassifySourceOrNull(x)!))
            .GroupBy(x => x.Group, (group, items) => new MaxIndentation
            (
                type: group,
                max_ident: items.MaxBy(x => x.Info.MaxLevel)!
            ));

    public static IEnumerable<UnhandledExtension> ListUnhandledExtensions(IEnumerable<FileInfo> files)
        => files
            .Where(x => FileUtil.ClassifySourceOrNull(x) == null)
            .Select(x => x.Extension)
            .GroupBy(x => x, (key, items) => new UnhandledExtension(extension: key, count: items.Count()))
            .OrderByDescending(x => x.Count);

    public static FileIndent CollectIndentInformation(FileInfo f, int tab_width, bool enable_javadoc_hack)
    {
        var lines = File.ReadAllLines(f.FullName, System.Text.Encoding.UTF8).ToImmutableArray();
        var is_autogenerated = FileUtil.LooksAutoGenerated(lines);

        if (is_autogenerated)
        {
            return new FileIndent { Min = 0, Max = 0, MaxLevel = 0, MaxLine = -1 };
        }

        var indentations = lines
            .Select(line => F.IndentationForLine(line, tab_width, enable_javadoc_hack))
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
                            return indent -= 1;
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
}

internal class LineIndent
{
    public int Indentation { get; set; }
    public int Line { get; set; }
}
