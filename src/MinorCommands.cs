using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Workbench.CMake;

namespace Workbench.MinorCommands;

public static class Main
{
    internal static void ConfigureCat(IConfigurator config, string v)
    {
        config.AddCommand<CatCommand>(v).WithDescription("Print the contents of a single file");
    }

    internal static void ConfigureDebug(IConfigurator config, string v)
    {
        config.AddCommand<DebugCommand>(v).WithDescription("Display what workbench think of your current setup");
    }

    internal static void ConfigureLs(IConfigurator config, string v)
    {
        config.AddCommand<LsCommand>(v).WithDescription("Print the tree of a directory");
    }
}

internal sealed class CatCommand : Command<CatCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("File to print")]
        [CommandArgument(0, "<input file>")]
        public string Path { get; set; } = "";
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print =>
        {
            print.cat(settings.Path);
            return 0;
        });
    }
}

internal sealed class DebugCommand : Command<DebugCommand.Arg>
{
    public sealed class Arg : CompileCommands.MainCommandSettings
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print =>
        {
            F.handle_debug(print, settings);
            return 0;
        });
    }
}

internal sealed class LsCommand : Command<LsCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Directoy to list")]
        [CommandArgument(0, "<input file>")]
        public string Path { get; set; } = "";
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print =>
        {
            print.ls(settings.Path);
            return 0;
        });
    }
}


internal static class F
{
    private static void print_found_list(Printer printer, string name, List<Found> list)
    {
        var found = Found.first_value_or_none(list) ?? "<None>";
        printer.Info($"{name}: {found}");
        foreach (var f in list)
        {
            printer.Info($"    {f}");
        }
    }

    internal static void handle_debug(Printer printer, CompileCommands.MainCommandSettings cc)
    {
        print_found_list(printer, "cmake", CmakeTools.list_all(printer).ToList());

        var root = Environment.CurrentDirectory;
        printer.Info($"Root: {root}");

        var project_build_folder = CompileCommands.Utils.find_build_root(root);
        if (project_build_folder is null)
        {
            printer.error("unable to find build folder");
        }
        else
        {
            printer.Info($"Project build folder: {project_build_folder}");
        }

        var ccs = cc.get_argument_or_none_with_cwd();
        if (ccs != null)
        {
            printer.Info($"Compile commands: {ccs}");
        }
        else
        {
            printer.Info("Compile commands: <NONE>");
        }
    }
}
