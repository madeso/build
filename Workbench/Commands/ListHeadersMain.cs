using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Workbench.ListHeaders;

namespace Workbench.Commands.ListHeadersCommands;



internal sealed class LinesCommand : Command<LinesCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("File to list lines in")]
        [CommandArgument(0, "<filename>")]
        public string FileName { get; set; } = string.Empty;

        [Description("The action to perform on the file")]
        [CommandArgument(1, "<action>")]
        public ListAction Action { get; set; } = ListAction.Lines;

        [Description("Git root to use")]
        [CommandArgument(0, "<git root>")]
        public string Root { get; set; } = "";
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter
            (
                print => { F.HandleLines(print, settings.FileName, settings.Action); return 0; }
            );
    }
}

internal sealed class FilesCommand : Command<FilesCommand.Arg>
{
    public sealed class Arg : CompileCommandsArguments
    {
        [Description("project file")]
        [CommandArgument(0, "<source>")]
        public List<string> Sources { get; set; } = new();

        [Description("number of most common includes to print")]
        [CommandOption("--count")]
        [DefaultValue(10)]
        public int MostCommonCount { get; set; }

        [Description("print debug info")]
        [CommandOption("--debug")]
        [DefaultValue(false)]
        public bool PrintDebugInfo { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        return CommonExecute.WithPrinter(print => F.HandleFiles(print, arg.GetPathToCompileCommandsOrNull(print), arg.Sources, arg.MostCommonCount, arg.PrintDebugInfo));
    }
}


internal class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, git =>
        {
            git.SetDescription("Tool to list headers");
            git.AddCommand<LinesCommand>("lines").WithDescription("List lines in a single file");
            git.AddCommand<FilesCommand>("files").WithDescription("Display includeded files from one or more source files");
        });
    }
}
