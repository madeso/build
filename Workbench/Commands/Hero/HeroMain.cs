using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;
using Workbench.Shared;

namespace Workbench.Commands.Hero;


public static class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, git =>
        {
            git.SetDescription("Port of the Header Hero app");
            git.AddCommand<NewHeroCommand>("new").WithDescription("Create a new header hero project");
            git.AddCommand<RunHeroHtmlCommand>("run-html").WithDescription("Analyze sources and generate a html report");
            git.AddCommand<RunHeroDotCommand>("run-dot").WithDescription("Analyze sources and generate a graphviz/dot report");
        });
    }
}

internal sealed class NewHeroCommand : Command<NewHeroCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Hero project to create")]
        [CommandArgument(0, "<input file>")]
        public string ProjectFile { get; set; } = "";

        [Description("If output exists, force overwrite")]
        [CommandOption("--overwrite")]
        [DefaultValue(false)]
        public bool Overwrite { get; init; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CliUtil.PrintErrorsAtExit(print =>
            UiFacade.HandleNewHero(settings.ProjectFile, settings.Overwrite, print));
    }
}

internal sealed class RunHeroHtmlCommand : Command<RunHeroHtmlCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Hero project file")]
        [CommandArgument(0, "<input file>")]
        public string ProjectFile { get; set; } = "";

        [Description("Html output directory")]
        [CommandArgument(0, "<output directory>")]
        public string OutputDirectory { get; set; } = "";
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var cwd = Dir.CurrentDirectory;
        var vfs = new VfsDisk();

        return CliUtil.PrintErrorsAtExit(print =>
        {
            var project_file = Cli.RequireFile(vfs, cwd, print, settings.ProjectFile, "project file");
            if (project_file == null)
            {
                return -1;
            }
            return UiFacade.HandleRunHeroHtml(vfs, cwd, project_file, Cli.ToOutputDirectory(cwd, settings.OutputDirectory),
                print);
        });
    }
}


internal sealed class RunHeroDotCommand : Command<RunHeroDotCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Hero project file")]
        [CommandArgument(0, "<input file>")]
        public string ProjectFile { get; set; } = "";

        [Description("Graphviz output file")]
        [CommandArgument(0, "<graphviz file>")]
        public string OutputFile { get; set; } = "";

        [Description("Simplify the graphiz output")]
        [CommandOption("--simplify")]
        [DefaultValue(false)]
        public bool SimplifyGraphviz { get; init; }

        [Description("Display only headers in the output")]
        [CommandOption("--only-headers")]
        [DefaultValue(false)]
        public bool OnlyHeaders { get; init; }

        [Description("Cluster files based on parent folder")]
        [CommandOption("--cluster")]
        [DefaultValue(false)]
        public bool Cluster { get; init; }

        [Description("Exclude some files")]
        [CommandOption("--exclude")]
        public string[]? Exclude { get; init; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg args)
        => CliUtil.PrintErrorsAtExit(print =>
        {
            var cwd = Dir.CurrentDirectory;
            var vfs = new VfsDisk();

            var project_file = Cli.RequireFile(vfs, cwd, print, args.ProjectFile, "project file");
            if (project_file == null)
            {
                return -1;
            }

            var exclude = Cli.ToExistingFileOrDir(cwd, args.Exclude, print);
            if (exclude == null)
            {
                return -1;
            }

            return UiFacade.RunHeroGraphviz(vfs,
                cwd, project_file,
                Cli.ToSingleFile(cwd, args.OutputFile, ""),
                args.SimplifyGraphviz,
                args.OnlyHeaders,
                args.Cluster,
                exclude,
                print);
        });
}
