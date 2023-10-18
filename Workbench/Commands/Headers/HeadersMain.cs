using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;
using Workbench.Commands.CheckIncludeOrder;
using Workbench.Utils;

namespace Workbench.Commands.Headers;


internal class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, git =>
        {
            git.SetDescription("Tool to list headers");
            git.AddCommand<LinesCommand>("list-lines").WithDescription("List lines in a single file");
            git.AddCommand<FilesCommand>("list-files").WithDescription("Display includeded files from one or more source files");

            git.AddCommand<IncludeListCommand>("include-list");
            git.AddCommand<IncludeGraphvizCommand>("include-gv");
        });
    }
}



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
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return Printer.PrintErrorsAtExit
            (
                print => { ListHeaderFunctions.HandleLines(print, settings.FileName, settings.Action); return 0; }
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
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        return Printer.PrintErrorsAtExit(print => ListHeaderFunctions.HandleFiles(print, arg.GetPathToCompileCommandsOrNull(print), arg.Sources, arg.MostCommonCount));
    }
}


[Description("list headers from files")]
internal sealed class IncludeListCommand : Command<IncludeListCommand.Arg>
{
    public sealed class Arg : CompileCommandsArguments
    {
        [Description("File to read")]
        [CommandArgument(0, "<input files>")]
        public string[] Files { get; set; } = Array.Empty<string>();

        [CommandOption("--print")]
        [DefaultValue(false)]
        public bool PrintFiles { get; set; } = false;

        [CommandOption("--print-stats")]
        [DefaultValue(false)]
        public bool PrintStats { get; set; } = false;

        [CommandOption("--print-max")]
        [DefaultValue(false)]
        public bool PrintMax { get; set; } = false;

        [CommandOption("--no-list")]
        [DefaultValue(true)]
        public bool PrintList { get; set; } = true;

        [CommandOption("--count")]
        [DefaultValue(2)]
        [Description("only print.Info includes that are more or equal to <count>")]
        public int Count { get; set; } = 2;
        

        [CommandOption("--limit")]
        [Description("limit search to theese files and folders")]
        public string[] Limit { get; set; } = Array.Empty<string> ();
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return Printer.PrintErrorsAtExit(print => Includes.HandleListIncludesCommand(print, settings.GetPathToCompileCommandsOrNull(print),
            settings.Files, settings.PrintFiles, settings.PrintStats, settings.PrintMax,
            settings.PrintList, settings.Count, settings.Limit));
    }
}




[Description("generate a graphviz of the includes")]
internal sealed class IncludeGraphvizCommand : Command<IncludeGraphvizCommand.Arg>
{
    public sealed class Arg : CompileCommandsArguments
    {
        [Description("File to read")]
        [CommandArgument(0, "<input files>")]
        public string[] Files { get; set; } = Array.Empty<string>();

        [CommandOption("--limit")]
        [Description("limit search to theese files and folders")]
        public string[] Limit { get; set; } = Array.Empty<string> ();

        [CommandOption("--group")]
        [Description("group output")]
        [DefaultValue(false)]
        public bool Group { get; set; } = false;

        [CommandOption("--cluster")]
        [Description("group output into clusters")]
        [DefaultValue(false)]
        public bool Cluster { get; set; } = false;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return Printer.PrintErrorsAtExit(print =>
            Includes.HandleIncludeGraphvizCommand(print, settings.GetPathToCompileCommandsOrNull(print),
                settings.Files, settings.Limit, settings.Group, settings.Cluster));
    }
}

