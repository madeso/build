using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;
using Workbench.Shared;

namespace Workbench.Commands.Clang;



internal sealed class MakeCommand : Command<MakeCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("don't write anything")]
        [CommandOption("--nop")]
        [DefaultValue(false)]
        public bool Nop { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        ClangFacade.HandleMakeTidyCommand(settings.Nop);
        return 0;
    }
}

internal sealed class ListCommand : Command<ListCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("sort listing")]
        [CommandOption("--sort")]
        [DefaultValue(false)]
        public bool Sort { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return Log.PrintErrorsAtExit(print => ClangFacade.HandleTidyListFilesCommand(print, settings.Sort));
    }
}

internal sealed class TidyCommand : Command<TidyCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("combinatory filter on what to run")]
        [CommandArgument(0, "<filter>")]
        public string[] Filter { get; set; } = Array.Empty<string>();

        [Description("don't do anything")]
        [CommandOption("--nop")]
        [DefaultValue(false)]
        public bool Nop { get; set; }

        [Description("try to fix the source")]
        [CommandOption("--fix")]
        [DefaultValue(false)]
        public bool Fix { get; set; }

        [Description("use shorter and stop after one file")]
        [CommandOption("--short")]
        [DefaultValue(false)]
        public bool Short { get; set; }

        [Description("also list files in the summary")]
        [CommandOption("--list")]
        [DefaultValue(false)]
        public bool List { get; set; }

        [Description("don't tidy headers")]
        [CommandOption("--no-headers")]
        [DefaultValue(false)]
        public bool Headers { get; set; }

        [Description("Force clang-tidy to run, even if there is a result")]
        [CommandOption("--force")]
        [DefaultValue(true)]
        public bool Force { get; set; }

        [Description("Only tidy files matching theese")]
        [CommandOption("--only")]
        [DefaultValue(null)]
        public string[]? Only { get; set; }

        [Description("the clang-tidy to use")]
        [CommandOption("--tidy")]
        [DefaultValue("clang-tidy")]
        public string ClangTidy { get; set; } = string.Empty;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return Log.PrintErrorsAtExit(print => ClangFacade.HandleRunClangTidyCommand(
            print,
            settings.ClangTidy,
            settings.Force,
            settings.Headers,
            settings.Short,
            settings.Nop,
            settings.Filter,
            settings.Only ?? Array.Empty<string>(),
            settings.Fix));
    }
}

internal sealed class FormatCommand : Command<FormatCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("don't format anything")]
        [CommandOption("--nop")]
        [DefaultValue(false)]
        public bool Nop { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return Log.PrintErrorsAtExit(print => ClangFacade.HandleClangFormatCommand(print, settings.Nop));
    }
}

public static class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, git =>
        {
            git.SetDescription("clang-tidy and clang-format related tools");
            git.AddCommand<MakeCommand>("make").WithDescription("make .clang-tidy");
            git.AddCommand<ListCommand>("ls").WithDescription("list files");
            git.AddCommand<TidyCommand>("tidy").WithDescription("do clang tidy on files");
            git.AddCommand<FormatCommand>("format").WithDescription("do clang format on files");
        });
    }
}
