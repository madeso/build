using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Workbench.Shared;
using Workbench.Shared.Extensions;

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
        public bool IncludeSources { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        var dir = new Dir(arg.Dir);
        foreach (var file in FileUtil.IterateFiles(dir, false, true)
            .Where(file => arg.IncludeSources ? FileUtil.IsHeaderOrSource(file) : FileUtil.IsHeader(file))
        )
        {
            AnsiConsole.WriteLine($"File: {file.GetDisplay()}");
            foreach (var line in file.ReadAllLines())
            {
                if (string.IsNullOrWhiteSpace(line)) { continue; }
                AnsiConsole.WriteLine($"    {line}");
            }
        }
        return 0;
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
        if (File.Exists(settings.Path))
        {
            AnsiConsole.MarkupLineInterpolated($"{settings.Path}>");
            foreach (var line in File.ReadAllLines(settings.Path))
            {
                AnsiConsole.MarkupLineInterpolated($"---->{line}");
            }
        }
        else
        {
            AnsiConsole.MarkupLineInterpolated($"Failed to open '{settings.Path}'");
        }

        return 0;
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

    // todo(Gustav): transform into a spectre tree
    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        print_recursive(Cli.ToDirectory(settings.Path), "");
        return 0;

        static void print_recursive(Dir root, string start)
        {
            var ident = " ".Repeat(4);

            foreach (var file_path in root.EnumerateDirectories())
            {
                AnsiConsole.MarkupLineInterpolated($"{start}{file_path.Name}/");
                print_recursive(file_path, $"{start}{ident}");
            }

            foreach (var file_path in root.EnumerateFiles())
            {
                AnsiConsole.MarkupLineInterpolated($"{start}{file_path.Name}");
            }
        }
    }
}
