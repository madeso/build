using Spectre.Console;
using Spectre.Console.Cli;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Workbench.Indent;

namespace Workbench.Commands.IndentCommands;

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
        var folder = settings.SearchPath ?? Directory.GetCurrentDirectory();

        var dir = new DirectoryInfo(folder);
        if (dir.Exists == false)
        {
            AnsiConsole.MarkupLineInterpolated($"[blue]ERROR[/]: Unable to open [red]{dir.FullName}[/]");
            return -1;
        }
        var files = FileUtil.IterateFiles(dir, settings.IncludeHidden, settings.Recursive)
            .ToImmutableArray();

        var grouped = F.GroupExtensionsWithMaxIndent(settings.TabWidth, settings.EnableJavadocHack, files);
        foreach (var f in grouped)
        {
            if (f.MaxIdent == null) { continue; }
            AnsiConsole.MarkupLineInterpolated($"Max ident [red]{f.MaxIdent.Info.Max}[/] ([red]{f.MaxIdent.Info.MaxLevel}[/]) for [blue]{f.Type}[/] in [blue]{f.MaxIdent.File}[/] ({f.MaxIdent.Info.MaxLine})");
        }

        if (settings.PrintUnused)
        {
            var unhandled = F.ListUnhandledExtensions(files);
            foreach (var x in unhandled)
            {
                AnsiConsole.MarkupLineInterpolated($"[green]{x.Extension}[/] - [blue]{x.Count}[/]");
            }
        }

        return 0;
    }
}

public static class Main
{
    public static void Configure(IConfigurator config, string name)
    {
        config.AddCommand<IndentationCommand>(name).WithDescription("Gets the max indentation level for source files");
    }
}
