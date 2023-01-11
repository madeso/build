using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;
using Workbench.Git;

namespace Workbench.Hero;

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
        return CommonExecute.WithPrinter(print =>
        {
            return CommonExecute.WithPrinter(print =>
            {
                if(File.Exists(settings.ProjectFile) && settings.Overwrite == false)
                {
                    print.error($"{settings.ProjectFile} already exists.");
                    return -1;
                }
                var input = new Data.UserInput();
                input.include_directories.Add("list of relative or absolute directories");
                input.project_directories.Add("list of relative or absolute source directories (or files)");
                input.precompiled_headers.Add("list of relative pchs, if there are any");

                var content = JsonUtil.Write(input);
                File.WriteAllText(settings.ProjectFile, content);
                return 0;
            });
        });
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
        return CommonExecute.WithPrinter(print =>
        {
            return CommonExecute.WithPrinter(print =>
            {
                var input = Data.UserInput.load_from_file(print, settings.ProjectFile);
                if (input == null)
                {
                    return -1;
                }
                var inputRoot = new FileInfo(settings.ProjectFile).DirectoryName ?? Environment.CurrentDirectory;
                input.decorate(print, inputRoot);
                Directory.CreateDirectory(settings.OutputDirectory);
                Ui.scan_and_generate_html(print, input, new(inputRoot, settings.OutputDirectory));
                return 0;
            });
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
        public bool Cluster{ get; init; }

        [Description("Exclude some files")]
        [CommandOption("--exclude")]
        public string[]? Exclude { get; init; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg args)
    {
        return CommonExecute.WithPrinter(print =>
        {
            return CommonExecute.WithPrinter(print =>
            {
                var input = Data.UserInput.load_from_file(print, args.ProjectFile);
                if (input == null)
                {
                    return -1;
                }
                var inputRoot = new FileInfo(args.ProjectFile).DirectoryName ?? Environment.CurrentDirectory;
                input.decorate(print, inputRoot);
                Ui.scan_and_generate_dot(print, input, new(inputRoot, args.OutputFile), args.SimplifyGraphviz, args.OnlyHeaders, args.Exclude ?? Array.Empty<string>(), args.Cluster);
                return 0;
            });
        });
    }
}

