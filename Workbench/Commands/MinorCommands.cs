using System.Collections;
using System.Collections.Immutable;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Workbench.Shared;
using Workbench.Shared.Extensions;
using System.Globalization;

namespace Workbench.Commands;

internal static class MinorCommands
{
    internal static void ConfigureCat(IConfigurator config, string v)
    {
        config.AddCommand<CatCommand>(v).WithDescription("Print the contents of a single file");
    }

    internal static void ConfigureCatDir(IConfigurator config, string v)
    {
        config.AddCommand<CatDirCommand>(v).WithDescription("Print the contents of a single directory");
    }

    internal static void ConfigureLs(IConfigurator config, string v)
    {
        config.AddCommand<LsCommand>(v).WithDescription("Print the tree of a directory");
    }

    internal static void ConfigureRemoveEmoji(IConfigurator config, string v)
    {
        config.AddCommand<RemoveEmojiCommand>(v).WithDescription("Remove all emojis from a file");
    }
}

internal sealed class CatDirCommand : Command<CatDirCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Directory to print")]
        [CommandArgument(0, "<input dir>")]
        public string Dir { get; set; } = "";

        [Description("Display all sources instead of just headers")]
        [CommandOption("--all")]
        [DefaultValue(false)]
        public bool IncludeSources { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        var cwd = Dir.CurrentDirectory;
        var dir = new Dir(arg.Dir);
        var vfs = new VfsDisk();

        foreach (var file in FileUtil.IterateFiles(vfs, dir, false, true)
            .Where(file => arg.IncludeSources ? FileUtil.IsHeaderOrSource(file) : FileUtil.IsHeader(file))
        )
        {
            AnsiConsole.WriteLine($"File: {file.GetDisplay(cwd)}");
            foreach (var line in file.ReadAllLines(vfs))
            {
                if (string.IsNullOrWhiteSpace(line)) { continue; }
                AnsiConsole.WriteLine($"    {line}");
            }
        }
        return 0;
    }
}

internal sealed class CatCommand : Command<CatCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("File to print")]
        [CommandArgument(0, "<input file>")]
        public string Path { get; set; } = "";
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        if (File.Exists(settings.Path))
        {
            AnsiConsole.MarkupLineInterpolated($"{settings.Path}>");
            foreach (var line in File.ReadAllLines(settings.Path))
            {
                AnsiConsole.MarkupLineInterpolated($"---->{line}");
            }
        }
        else
        {
            AnsiConsole.MarkupLineInterpolated($"Failed to open '{settings.Path}'");
        }

        return 0;
    }
}

internal sealed class RemoveEmojiCommand : Command<RemoveEmojiCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("File to remove emoji from")]
        [CommandArgument(0, "<input file>")]
        public string Path { get; set; } = "";
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        if (File.Exists(settings.Path))
        {
            var lines = RemoveEmojiFrom(File.ReadAllLines(settings.Path)).ToImmutableArray();
            File.WriteAllLines(settings.Path, lines);
        }
        else
        {
            AnsiConsole.MarkupLineInterpolated($"Failed to open '{settings.Path}'");
        }

        return 0;
    }

    private IEnumerable<string> RemoveEmojiFrom(IEnumerable<string> lines)
    {
        return lines.Select(RemoveEmojiFromString);
    }

    private string RemoveEmojiFromString(string arg)
    {
        var sb = new System.Text.StringBuilder();
        var si = new StringInfo(arg);
        for (int i = 0; i < si.LengthInTextElements; i++)
        {
            string element = si.SubstringByTextElements(i, 1);
            int codepoint = char.ConvertToUtf32(element, 0);

            // Filter out emoji codepoints (common blocks)
            if (
                codepoint is >= 0x1F600 and <= 0x1F64F || // Emoticons
                codepoint is >= 0x1F300 and <= 0x1F5FF || // Misc Symbols and Pictographs
                codepoint is >= 0x1F680 and <= 0x1F6FF || // Transport and Map
                codepoint is >= 0x02600 and <= 0x026FF || // Misc symbols
                codepoint is >= 0x02700 and <= 0x027BF || // Dingbats
                codepoint is >= 0x1F900 and <= 0x1F9FF || // Supplemental Symbols and Pictographs
                codepoint is >= 0x1FA70 and <= 0x1FAFF || // Symbols and Pictographs Extended-A
                codepoint is >= 0x1F1E6 and <= 0x1F1FF    // Flags
            )
            {
                continue; // skip emoji
            }
            sb.Append(element);
        }
        return sb.ToString();
    }
}

internal sealed class LsCommand : Command<LsCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Directoy to list")]
        [CommandArgument(0, "[input directory]")]
        public string Path { get; set; } = "";

        [Description("Recursivly list")]
        [CommandOption("--recursive")]
        [DefaultValue(false)]
        public bool Recursive { get; set; } = false;

        [Description("Max depth")]
        [CommandOption("--depth")]
        [DefaultValue(3)]
        public int MaxDepth { get; set; } = 3;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var cwd = Dir.CurrentDirectory;
        var vfs = new VfsDisk();

        return CliUtil.PrintErrorsAtExit(log =>
        {
            var dir = Cli.RequireDirectory(vfs, cwd, log, settings.Path, "path");
            if (dir == null)
            {
                return -1;
            }

            var tree = new Tree(dir.Name);
            print_recursive(vfs, tree, dir, settings.Recursive, 0, settings.MaxDepth);
            AnsiConsole.Write(tree);
            return 0;

            static void print_recursive(Vfs vfs, IHasTreeNodes tree, Dir root, bool recursive, int current_depth, int max_depth)
            {
                if (max_depth >= 0 && current_depth >= max_depth)
                {
                    if (root.EnumerateDirectories(vfs).Any() || root.EnumerateFiles(vfs).Any())
                    {
                        tree.AddNode("...");
                    }
                    return;
                }
                foreach (var file_path in root.EnumerateDirectories(vfs))
                {
                    var n = tree.AddNode(Cli.ToMarkup($"[blue]{file_path.Name}[/]/"));
                    if(recursive)
                    {
                        print_recursive(vfs, n, file_path, recursive, current_depth+1, max_depth);
                    }
                }
                

                foreach (var file_path in root.EnumerateFiles(vfs))
                {
                    tree.AddNode(Cli.ToMarkup($"[red]{file_path.Name}[/]"));
                }
            }
        });
    }
}
