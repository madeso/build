using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;
using Workbench.Shared;

namespace Workbench.Commands.Indent;

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

        [CommandOption("--no-javadoc-hack")]
        [DefaultValue(true)]
        public bool EnableJavadocHack { get; init; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
    {
        var cwd = Dir.CurrentDirectory;
        var folder = settings.SearchPath ?? Directory.GetCurrentDirectory();
        var vfs = new VfsDisk();

        var dir = new Dir(folder);
        if (dir.Exists == false)
        {
            AnsiConsole.MarkupLineInterpolated($"[blue]ERROR[/]: Unable to open [red]{dir.GetDisplay(cwd)}[/]");
            return -1;
        }
        var files = FileUtil.IterateFiles(dir, settings.IncludeHidden, settings.Recursive)
            .ToImmutableArray();

        var grouped = IndentFunctions.GroupExtensionsWithMaxIndent(vfs, settings.TabWidth, settings.EnableJavadocHack, files);
        foreach (var f in grouped)
        {
            AnsiConsole.MarkupLineInterpolated($"Max ident [red]{f.MaxIdent.Info.Max}[/] ([red]{f.MaxIdent.Info.MaxLevel}[/]) for [blue]{f.Type}[/] in [blue]{f.MaxIdent.File}[/] ({f.MaxIdent.Info.MaxLine})");
        }

        if (settings.PrintUnused)
        {
            var unhandled = IndentFunctions.ListUnhandledExtensions(files);
            foreach (var x in unhandled)
            {
                AnsiConsole.MarkupLineInterpolated($"[green]{x.Extension}[/] - [blue]{x.Count}[/]");
            }
        }

        return 0;
    }
}


[Description("list the files with the maximum indents")]
internal sealed class ListIndentsCommand : Command<ListIndentsCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("File to read")]
        [CommandArgument(0, "<input files>")]
        public string[] Files { get; set; } = Array.Empty<string>();

        [CommandOption("--each")]
        [Description("group counts")]
        [DefaultValue(1)]
        public int Each { get; set;} = 0;

        [CommandOption("--show")]
        [Description("include files in list")]
        public bool Show { get; set; } = false;

        [CommandOption("--hist")]
        [Description("show simple histogram")]
        public bool DisplayHistogram { get; set; } = false;

        [CommandOption("--include-empty")]
        [DefaultValue(true)]
        public bool DiscardEmpty { get; set; } = true;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var cwd = Dir.CurrentDirectory;
        var vfs = new VfsDisk();

        return IndentFunctions.HandleListIndents(vfs, cwd, settings.Files, settings.Each, settings.Show, settings.DisplayHistogram, settings.DiscardEmpty);
    }
}

public static class Main
{
    public static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, cmake =>
        {
            cmake.SetDescription("Indent commands that should be merged to a single");
            cmake.AddCommand<IndentationCommand>("root").WithDescription("Gets the max indentation level for source files");
            cmake.AddCommand<ListIndentsCommand>("tools");
        });
    }
}
