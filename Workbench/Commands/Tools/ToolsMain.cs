using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;
using Workbench.CMake;

namespace Workbench.Commands.Tools;

/*

Previous commands for refactoring info:
wb tools check-missing-in-cmake
wb tools check-missing-in-cmake libs
wb tools check-missing-in-cmake libs apps

wb tools list-no-project-folders libs apps
wb tools list-no-project-folders libs apps external
wb tools list-no-project-folders libs apps external tools data

wb tools check-missing-pragma-once libs apps
wb tools check-missing-pragma-once libs apps

*/


internal class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, cmake =>
        {
            cmake.SetDescription("various smaller tools for investigating code health and getting statistics");
            cmake.AddCommand<LineCountCommand>("line-count");

            cmake.AddCommand<IncludeListCommand>("include-list");
            cmake.AddCommand<IncludeGraphvizCommand>("include-gv");

            cmake.AddCommand<MissingPragmaOnceCommand>("check-missing-pragma-once");
            cmake.AddCommand<MissingInCmakeCommand>("check-missing-in-cmake");
            cmake.AddCommand<ListNoProjectFoldersCommand>("list-no-project-folders");
            cmake.AddCommand<CheckFileNamesCommand>("check-file-names");
        });
    }
}


[Description("list line counts")]
internal sealed class LineCountCommand : Command<LineCountCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("File to read")]
        [CommandArgument(0, "<input files>")]
        public string[] Files { get; set; } = Array.Empty<string>();

        [CommandOption("--each")]
        [DefaultValue(1)]
        public int Each { get; set; } = 1;

        [CommandOption("--show")]
        [DefaultValue(false)]
        public bool Show { get; set; } = false;

        [CommandOption("--include-empty")]
        [DefaultValue(true)]
        public bool DiscardEmpty { get; set; } = true;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print => Tools.HandleLineCountCommand(print, settings.Files,
            settings.Each, settings.Show, settings.DiscardEmpty));
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
        return CommonExecute.WithPrinter(print => Tools.HandleListIncludesCommand(print, settings.GetPathToCompileCommandsOrNull(print),
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
        return CommonExecute.WithPrinter(print =>
            Tools.HandleIncludeGraphvizCommand(print, settings.GetPathToCompileCommandsOrNull(print),
                settings.Files, settings.Limit, settings.Group, settings.Cluster));
    }
}




[Description("find headers with missing include guards")]
internal sealed class MissingPragmaOnceCommand : Command<MissingPragmaOnceCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("File to read")]
        [CommandArgument(0, "<input files>")]
        public string[] Files { get; set; } = Array.Empty<string>();
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print => Tools.MissingIncludeGuardsCommand(print, settings.Files));
    }
}




[Description("find files that exists on disk but are missing in cmake")]
internal sealed class MissingInCmakeCommand : Command<MissingInCmakeCommand.Arg>
{
    public sealed class Arg : CompileCommandsArguments
    {
        [Description("File to read")]
        [CommandArgument(0, "<input files>")]
        public string[] Files { get; set; } = Array.Empty<string>();
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print =>
        {
            var cmake = CmakeTools.FindInstallationOrNull(print);
            if (cmake == null)
            {
                print.Error("Failed to find cmake");
                return -1;
            }

            return Tools.HandleMissingInCmakeCommand(print, settings.Files, CMake.CmakeTools.FindBuildOrNone(settings, print), cmake);
        });
    }
}




[Description("find projects that have not set the solution folder")]
internal sealed class ListNoProjectFoldersCommand : Command<ListNoProjectFoldersCommand.Arg>
{
    public sealed class Arg : CompileCommandsArguments
    {
        [Description("File to read")]
        [CommandArgument(0, "<input files>")]
        public string[] Files { get; set; } = Array.Empty<string>();
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print =>
        {
            var cmake = CmakeTools.FindInstallationOrNull(print);
            if (cmake == null)
            {
                print.Error("Failed to find cmake");
                return -1;
            }

            var build_root = CmakeTools.FindBuildOrNone(settings, print);
            if (build_root == null)
            {
                return -1;
            }

            return Tools.HandleListNoProjectFolderCommand(print, settings.Files, build_root, cmake);
        });
    }
}




[Description("find files that doesn't match the name style")]
internal sealed class CheckFileNamesCommand : Command<CheckFileNamesCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("File to read")]
        [CommandArgument(0, "<input files>")]
        public string[] Files { get; set; } = Array.Empty<string>();
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print => Tools.HandleCheckFilesCommand(print, settings.Files));
    }
}

