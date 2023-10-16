using Spectre.Console.Cli;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Workbench.ListHeaders;
using Workbench.Utils;

namespace Workbench.Commands;

internal static class MinorCommands
{
    internal static void ConfigureCat(IConfigurator config, string v)
    {
        config.AddCommand<CatCommand>(v).WithDescription("Print the contents of a single file");
    }

    internal static void ConfigureCatDir(IConfigurator config, string v)
    {
        config.AddCommand<CatDirCommand>(v).WithDescription("Print the contents of a single directory");
    }

    internal static void ConfigureLs(IConfigurator config, string v)
    {
        config.AddCommand<LsCommand>(v).WithDescription("Print the tree of a directory");
    }
}

internal sealed class CatDirCommand : Command<CatDirCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Directory to print")]
        [CommandArgument(0, "<input dir>")]
        public string Dir { get; set; } = "";

        [Description("Display all sources instead of just headers")]
        [CommandOption("--all")]
        [DefaultValue(false)]
        public bool AllSources { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        return CommonExecute.WithPrinter(print =>
        {
            var dir = new DirectoryInfo(arg.Dir);
            foreach (var file in FileUtil.IterateFiles(dir, false, true)
                .Where(file => FileUtil.FileHasAnyExtension(file.FullName, arg.AllSources ? FileUtil.HeaderAndSourceFiles : FileUtil.HeaderFiles))
            )
            {
                Printer.Info($"File: {file.FullName}");
                foreach (var line in File.ReadAllLines(file.FullName))
                {
                    if (string.IsNullOrWhiteSpace(line)) { continue; }
                    Printer.Info($"    {line}");
                }
            }
            return 0;
        });
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
            Printer.PrintContentsOfFile(settings.Path);
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
            Printer.PrintDirectoryStructure(settings.Path);
            return 0;
        });
    }
}
