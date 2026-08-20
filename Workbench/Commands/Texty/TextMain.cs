using Spectre.Console;
using Spectre.Console.Cli;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Workbench.Shared;

namespace Workbench.Commands.Texty;

public static class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, cmake =>
        {
            cmake.SetDescription("Generic text-file related commands");
            cmake.AddCommand<RemoveEmojiCommand>("remove-emoji").WithDescription("Remove all emojis from a file");

            cmake.AddCommand<TrimCommand>("trim").WithDescription("Trim lines");
            cmake.AddCommand<RemoveEmptyCommand>("remove-empty").WithDescription("Remove empty lines");
            cmake.AddCommand<UniqueCommand>("unique").WithDescription("Remove duplicate lines");
            cmake.AddCommand<SortCommand>("sort").WithDescription("Sort lines");
            cmake.AddCommand<FilterCommand>("filter").WithDescription("filter things");
            cmake.AddCommand<ReverseCommand>("reverse").WithDescription("reverse lines order");
        });
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




internal sealed class TrimCommand : Command<TrimCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("File to trim lines from")]
        [CommandArgument(0, "<input file>")]
        public string Path { get; set; } = "";

        // todo(Gustav): add start/end/both option
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        if (!File.Exists(settings.Path))
        {
            AnsiConsole.MarkupLineInterpolated($"Failed to open '{settings.Path}'");
            return -1;
        }

        var src = File.ReadAllLines(settings.Path);
        
        var lines = src.Select(x => x.Trim());

        var dst = lines.ToImmutableArray();
        File.WriteAllLines(settings.Path, dst);
        return 0;
    }
}



internal sealed class RemoveEmptyCommand : Command<RemoveEmptyCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("File to remove empty lines from")]
        [CommandArgument(0, "<input file>")]
        public string Path { get; set; } = "";

        [Description("Also remove lines that are whitespace only")]
        [CommandOption("-w|--whitespace")]
        public bool IncludeWhitespace { get; set; } = false;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        if (!File.Exists(settings.Path))
        {
            AnsiConsole.MarkupLineInterpolated($"Failed to open '{settings.Path}'");
            return -1;
        }

        var src = File.ReadAllLines(settings.Path);

        var lines = src.Where(s => (settings.IncludeWhitespace ? string.IsNullOrWhiteSpace(s) : string.IsNullOrEmpty(s)) == false);

        var dst = lines.ToImmutableArray();
        File.WriteAllLines(settings.Path, dst);
        return 0;
    }
}



internal sealed class UniqueCommand : Command<UniqueCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("File to remove emoji from")]
        [CommandArgument(0, "<input file>")]
        public string Path { get; set; } = "";
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        if (!File.Exists(settings.Path))
        {
            AnsiConsole.MarkupLineInterpolated($"Failed to open '{settings.Path}'");
            return -1;
        }

        var src = File.ReadAllLines(settings.Path);

        var lines = src.Distinct();

        var dst = lines.ToImmutableArray();
        File.WriteAllLines(settings.Path, dst);
        return 0;
    }
}



internal sealed class SortCommand : Command<SortCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("File to remove emoji from")]
        [CommandArgument(0, "<input file>")]
        public string Path { get; set; } = "";

        [CommandOption("-d|--direction")]
        [DefaultValue(OrderDirection.Ascending)]
        [Description("Order direction")]
        public OrderDirection Direction { get; set; } = OrderDirection.Ascending;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        if (!File.Exists(settings.Path))
        {
            AnsiConsole.MarkupLineInterpolated($"Failed to open '{settings.Path}'");
            return -1;
        }

        var src = File.ReadAllLines(settings.Path);

        var lines = src.Order().InDirection(settings.Direction);

        var dst = lines.ToImmutableArray();
        File.WriteAllLines(settings.Path, dst);
        return 0;
    }
}



internal sealed class FilterCommand : Command<FilterCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("File to remove lines from")]
        [CommandArgument(0, "<input file>")]
        public string Path { get; set; } = "";

        [Description("Filter used to exclude")]
        [CommandArgument(1, "<filter>")]
        public string Filter { get; set; } = "";
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        if (!File.Exists(settings.Path))
        {
            AnsiConsole.MarkupLineInterpolated($"Failed to open '{settings.Path}'");
            return -1;
        }

        var src = File.ReadAllLines(settings.Path);

        var lines = src.Where(x => x.Contains(settings.Filter));

        var dst = lines.ToImmutableArray();
        File.WriteAllLines(settings.Path, dst);
        return 0;
    }
}



internal sealed class ReverseCommand : Command<ReverseCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("File to reverse")]
        [CommandArgument(0, "<input file>")]
        public string Path { get; set; } = "";
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        if (!File.Exists(settings.Path))
        {
            AnsiConsole.MarkupLineInterpolated($"Failed to open '{settings.Path}'");
            return -1;
        }

        var src = File.ReadAllLines(settings.Path);

        var lines = src.Reverse();

        var dst = lines.ToImmutableArray();
        File.WriteAllLines(settings.Path, dst);
        return 0;
    }
}

