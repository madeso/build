﻿using Spectre.Console.Cli;
using Spectre.Console;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Workbench;


internal class IndentInformation
{
    public int Min { get; set; }
    public int Max { get; set; }
    public float MaxLevel { get; internal set; }
    public int MaxLine { get; internal set; }
}

internal sealed class IndentationCommand : Command<IndentationCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Path to search. Defaults to current directory.")]
        [CommandArgument(0, "[searchPath]")]
        public string? SearchPath { get; init; }

        [CommandOption("--unused")]
        [DefaultValue(false)]
        public bool PrintUnused { get; init; }

        [CommandOption("--tab-width")]
        [DefaultValue(4)]
        public int TabWidth { get; init; }

        [CommandOption("--hidden")]
        [DefaultValue(false)]
        public bool IncludeHidden { get; init; }

        [CommandOption("--no-recursive")]
        [DefaultValue(true)]
        public bool Recursive { get; init; }
    }

    IEnumerable<FileInfo> IterateFiles(DirectoryInfo root, EnumerationOptions searchOptions, bool directories)
    {
        foreach (var f in root.GetFiles("*", searchOptions))
        {
            yield return f;
        }

        if (directories)
        {
            foreach (var d in root.GetDirectories("*", searchOptions))
            {
                if (IsValidDirectory(d) == false) { continue; }
                foreach (var f in IterateFiles(d, searchOptions, true))
                {
                    yield return f;
                }
            }
        }
    }

    private bool IsValidDirectory(DirectoryInfo d)
    {
        switch (d.Name)
        {
            case ".git": return false;
            case "node_modules": return false;
            default: return true;
        }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        var folder = settings.SearchPath ?? Directory.GetCurrentDirectory();

        var searchOptions = new EnumerationOptions
        {
            AttributesToSkip = settings.IncludeHidden
                ? FileAttributes.Hidden | FileAttributes.System
                : FileAttributes.System
        };

        var files = IterateFiles(new DirectoryInfo(folder), searchOptions, settings.Recursive).ToImmutableArray();

        var unhandledExtensions = files
            .Where(x => ClassifySourceOrNull(x) == null)
            .Select(x => x.Extension)
            .GroupBy(x => x, (key, items) => new { Extension = key, Count = items.Count() })
            .OrderByDescending(x => x.Count)
            .ToImmutableArray()
            ;

        var grouped = files
            .Where(x => ClassifySourceOrNull(x) != null)
            .Select(x => new { File = x, Info = CollectIndentInformation(x, settings.TabWidth), Group = ClassifySourceOrNull(x) })
            .GroupBy(x => x.Group, (group, items) => new
            {
                Type = group,
                MaxIdent = items.MaxBy(x => x.Info.MaxLevel),
            })
            .ToImmutableArray()
            ;

        foreach (var f in grouped)
        {
            if (f.MaxIdent == null) { continue; }
            AnsiConsole.MarkupLine($"Max ident [red]{f.MaxIdent.Info.Max}[/] ([red]{f.MaxIdent.Info.MaxLevel}[/]) for [blue]{f.Type}[/] in [blue]{f.MaxIdent.File}[/] ({f.MaxIdent.Info.MaxLine})");
        }

        if (settings.PrintUnused)
        {
            foreach (var x in unhandledExtensions)
            {
                AnsiConsole.MarkupLine($"[green]{x.Extension}[/] - [blue]{x.Count}[/]");
            }
        }

        return 0;
    }

    private static IEnumerable<int> Integers(int start = 0)
    {
        while (true) yield return start++;
    }

    private static IndentInformation CollectIndentInformation(FileInfo f, int tabWidth)
    {
        var lines = File.ReadAllLines(f.FullName, System.Text.Encoding.UTF8).ToImmutableArray();

        var isAutogenerated = lines
            .Take(5)
            .Select(x => LineLooksLikeAutoGenerated(x))
            .Where(x => x)
            .FirstOrDefault(false)
            ;

        if (isAutogenerated)
        {
            return new IndentInformation { Min = 0, Max = 0, MaxLevel = 0, MaxLine = -1 };
        }

        var indentations = lines
            .Select(line => IndentationForLine(line, tabWidth))
            .Zip(Integers(1))
            .Select(x => new LineIndent { Indentation = x.First, Line = x.Second })
            .ToImmutableArray()
            ;
        var mine = indentations.Where(i => i.Indentation > 0).ToImmutableArray();
        var missing = new LineIndent { Indentation = 0, Line = -1 };
        var min = (mine.Length == 0 ? null : mine.MinBy(x => x.Indentation)) ?? missing;
        var max = (indentations.Length == 0 ? null : indentations.MaxBy(x => x.Indentation)) ?? missing;
        var ml = min.Indentation != 0 ? max.Indentation / (float)min.Indentation : 0.0f;
        return new IndentInformation { Min = min.Indentation, Max = max.Indentation, MaxLevel = ml, MaxLine = max.Line };
    }

    private static bool LineLooksLikeAutoGenerated(string line)
    {
        var lower = line.ToLowerInvariant();
        if (lower.Contains("auto-generated"))
        {
            return true;
        }

        if (lower.Contains("generated by"))
        {
            return true;
        }

        return false;
    }

    private static int IndentationForLine(string line, int tabWidth)
    {
        int indent = 0;
        foreach (var c in line)
        {
            if (c == ' ')
            {
                indent += 1;
            }
            else if (c == '\t')
            {
                indent += tabWidth;
            }
            else
            {
                return indent;
            }
        }

        return indent;
    }

    private static string? ClassifySourceOrNull(FileInfo f)
    {
        switch (f.Extension)
        {
            case ".cs":
                return "c#";

            case ".jsx":
                return "React";

            case ".ts":
            case ".js":
                return "Javascript/typescript";

            case ".cpp":
            case ".c":
            case ".h":
            case ".hpp":
                return "C/C++";

            default:
                return null;
        }
    }
}

internal class LineIndent
{
    public int Indentation { get; set; }
    public int Line { get; set; }
}
