using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;
using Workbench.Shared;

namespace Workbench.Commands.Clang;



internal sealed class MakeClangTidyCommand : Command<MakeClangTidyCommand.Arg>
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
        ClangTidyFile.HandleMakeTidyCommand(settings.Nop);
        return 0;
    }
}

internal sealed class ListClangTidyCommand : Command<ListClangTidyCommand.Arg>
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
        return Log.PrintErrorsAtExit(print => ClangFiles.HandleTidyListFilesCommand(print, settings.Sort));
    }
}

internal sealed class RunTidyCommand : AsyncCommand<RunTidyCommand.Arg>
{
    public sealed class Arg : CompileCommandsArguments
    {
        [Description("combinatory filter on what to run")]
        [CommandArgument(0, "[filter]")]
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
        [DefaultValue(false)]
        public bool Force { get; set; }

        [Description("Only tidy files matching theese")]
        [CommandOption("--only")]
        [DefaultValue(null)]
        public string[]? Only { get; set; }

        [Description("Html output directory")]
        [CommandOption("--html")]
        [DefaultValue(null)]
        public string? HtmlDir { get; set; }

        [Description("Number of parallell tasks")]
        [CommandOption("--tasks")]
        [DefaultValue(3)]
        public int NumberOfTasks { get; set; } = 3;
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        Dir? html_dir = null;

        if(settings.HtmlDir != null)
        {
            html_dir = Cli.ToDirPath(settings.HtmlDir);
            if(html_dir == null)
            {
                Console.WriteLine($"Failed to parse html ouput directory {settings.HtmlDir}");
                return -1;
            }
        }
        return await Log.PrintErrorsAtExitAsync(print => ClangTidy.HandleRunClangTidyCommand(
            settings, print,
            settings.Force,
            settings.Headers,
            settings.Short,
            settings.Nop,
            settings.Filter,
            settings.Only ?? Array.Empty<string>(),
            settings.Fix,
            settings.NumberOfTasks,
            html_dir));
    }
}

internal sealed class RunClangFormatCommand : AsyncCommand<RunClangFormatCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("don't format anything")]
        [CommandOption("--nop")]
        [DefaultValue(false)]
        public bool Nop { get; set; }
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return await Log.PrintErrorsAtExitAsync(async print =>
            await ClangFormat.HandleClangFormatCommand(print, settings.Nop));
    }
}

public static class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, git =>
        {
            git.SetDescription("clang-tidy and clang-format related tools");
            git.AddCommand<MakeClangTidyCommand>("make").WithDescription("Make .clang-tidy");
            git.AddCommand<ListClangTidyCommand>("ls").WithDescription("list files");
            git.AddCommand<RunTidyCommand>("tidy").WithDescription("Run clang tidy on files");
            git.AddCommand<RunClangFormatCommand>("format").WithDescription("Run clang format on files");
        });
    }
}
