using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Workbench.CheckIncludes;

namespace Workbench.Commands.CheckIncludesCommands;


internal class SharedArguments : CommandSettings
{
    [Description("Files to look at")]
    [CommandArgument(0, "<file>")]
    public string[] Files { get; set; } = Array.Empty<string>();

    [Description("Print general file status at the end")]
    [CommandOption("--status")]
    [DefaultValue(false)]
    public bool PrintStatusAtTheEnd { get; set; }

    [Description("Use verbose output")]
    [CommandOption("--verbose")]
    [DefaultValue(false)]
    public bool UseVerboseOutput { get; set; }

    internal CommonArgs ToCommon()
    {
        return new(Files, PrintStatusAtTheEnd, UseVerboseOutput);
    }
}


internal class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, cmake =>
        {
            cmake.SetDescription("Check the order of the #include statements");
            cmake.AddCommand<MissingPatternsCommand>("missing-patterns").WithDescription("Print headers that don't match any pattern so you can add more regexes");
            cmake.AddCommand<ListUnfixableCommand>("list-unfixable").WithDescription("Print headers that can't be fixed");
            cmake.AddCommand<CheckCommand>("check").WithDescription("Check for style errors and error out");
            cmake.AddCommand<FixCommand>("fix").WithDescription("Fix style errors and print unfixable");
        });
    }
}


internal sealed class MissingPatternsCommand : Command<MissingPatternsCommand.Arg>
{
    public sealed class Arg : SharedArguments
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithLoadedBuildData
            (
                (print, data) => IncludeTools.CommonMain(settings.ToCommon(), print, data, new CheckAction.MissingPatterns())
            );
    }
}


internal sealed class ListUnfixableCommand : Command<ListUnfixableCommand.Arg>
{
    public sealed class Arg : SharedArguments
    {
        [Description("Print all errors per file, not just the first one")]
        [CommandOption("--all")]
        [DefaultValue(false)]
        public bool PrintAllErrors { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithLoadedBuildData
            (
                (print, data) => IncludeTools.CommonMain(settings.ToCommon(), print, data, new CheckAction.ListUnfixable(settings.PrintAllErrors == false))
            );
    }
}


internal sealed class CheckCommand : Command<CheckCommand.Arg>
{
    public sealed class Arg : SharedArguments
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithLoadedBuildData
            (
                (print, data) => IncludeTools.CommonMain(settings.ToCommon(), print, data, new CheckAction.Check())
            );
    }
}


internal sealed class FixCommand : Command<FixCommand.Arg>
{
    public sealed class Arg : SharedArguments
    {
        [Description("Write fixes to file")]
        [CommandOption("--write")]
        [DefaultValue(false)]
        public bool WriteToFile { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithLoadedBuildData
            (
                (print, data) => IncludeTools.CommonMain(settings.ToCommon(), print, data, new CheckAction.Fix(settings.WriteToFile == false))
            );
    }
}