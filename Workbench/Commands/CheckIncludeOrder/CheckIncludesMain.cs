using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;
using Workbench.Shared;

namespace Workbench.Commands.CheckIncludeOrder;

public static class CheckIncludesCommonExecute
{
    public static int WithLoadedIncludeData(Func<Log, IncludeData, int> callback)
    {
        var cwd = Dir.CurrentDirectory;
        var vread = new ReadFromDisk();

        return CliUtil.PrintErrorsAtExit(print =>
        {
            var data = IncludeData.LoadOrNull(vread, cwd, print);
            if (data == null)
            {
                print.Error("Unable to load the data");
                return -1;
            }
            else
            {
                return callback(print, data.Value);
            }
        });
    }
}

internal class SharedArguments : CommandSettings
{
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
        return new(PrintStatusAtTheEnd, UseVerboseOutput);
    }
}


internal class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, cmake =>
        {
            cmake.SetDescription("Check the order of the #include statements");
            cmake.AddCommand<InitCommand>("init").WithDescription("Create a check includes command");
            cmake.AddCommand<MissingPatternsCommand>("missing-patterns").WithDescription("Print headers that don't match any pattern so you can add more regexes");
            cmake.AddCommand<ListUnfixableCommand>("list-unfixable").WithDescription("Print headers that can't be fixed");
            cmake.AddCommand<CheckCommand>("check").WithDescription("Check for style errors and error out");
            cmake.AddCommand<FixCommand>("fix").WithDescription("Fix style errors and print unfixable");
        });
    }
}

internal sealed class InitCommand : Command<InitCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("If output exists, force overwrite")]
        [CommandOption("--overwrite")]
        [DefaultValue(false)]
        public bool Overwrite { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var cwd = Dir.CurrentDirectory;
        var vwrite = new WriteToDisk();

        return CliUtil.PrintErrorsAtExit(print => IncludeTools.HandleInit(vwrite, cwd, print, settings.Overwrite));
    }
}


internal sealed class MissingPatternsCommand : Command<MissingPatternsCommand.Arg>
{
    public sealed class Arg : SharedArguments
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var cwd = Dir.CurrentDirectory;
        var vread = new ReadFromDisk();
        var vwrite = new WriteToDisk();

        return CheckIncludesCommonExecute.WithLoadedIncludeData
            (
                (print, data) => IncludeTools.CommonMain(vread, vwrite, cwd, settings.ToCommon(), print, data,
                    new CheckAction.MissingPatterns())
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
        var cwd = Dir.CurrentDirectory;
        var vread = new ReadFromDisk();
        var vwrite = new WriteToDisk();

        return CheckIncludesCommonExecute.WithLoadedIncludeData
            (
                (print, data) => IncludeTools.CommonMain(vread, vwrite, cwd, settings.ToCommon(), print, data, new CheckAction.ListUnfixable(settings.PrintAllErrors == false))
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
        var cwd = Dir.CurrentDirectory;
        var vread = new ReadFromDisk();
        var vwrite = new WriteToDisk();

        return CheckIncludesCommonExecute.WithLoadedIncludeData
            (
                (print, data) => IncludeTools.CommonMain(vread, vwrite, cwd, settings.ToCommon(), print, data, new CheckAction.Check())
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
        var cwd = Dir.CurrentDirectory;
        var vread = new ReadFromDisk();
        var vwrite = new WriteToDisk();

        return CheckIncludesCommonExecute.WithLoadedIncludeData
            (
                (print, data) => IncludeTools.CommonMain(vread, vwrite, cwd, settings.ToCommon(), print, data, new CheckAction.Fix(settings.WriteToFile == false))
            );
    }
}