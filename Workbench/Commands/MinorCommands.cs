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
        [CommandArgument(0, "[input directory]")]
        public string Path { get; set; } = "";

        [Description("Recursivly list")]
        [CommandOption("--recursive")]
        [DefaultValue(false)]
        public bool Recursive { get; set; } = false;

        [Description("Max depth")]
        [CommandOption("--depth")]
        [DefaultValue(3)]
        public int MaxDepth { get; set; } = 3;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CliUtil.PrintErrorsAtExit(log =>
        {
            var dir = Cli.RequireDirectory(log, settings.Path, "path");
            if (dir == null)
            {
                return -1;
            }

            var tree = new Tree(dir.Name);
            print_recursive(tree, dir, settings.Recursive, 0, settings.MaxDepth);
            AnsiConsole.Write(tree);
            return 0;

            static void print_recursive(IHasTreeNodes tree, Dir root, bool recursive, int current_depth, int max_depth)
            {
                if (max_depth >= 0 && current_depth >= max_depth)
                {
                    if (root.EnumerateDirectories().Any() || root.EnumerateFiles().Any())
                    {
                        tree.AddNode("...");
                    }
                    return;
                }
                foreach (var file_path in root.EnumerateDirectories())
                {
                    var n = tree.AddNode(Cli.ToMarkup($"[blue]{file_path.Name}[/]/"));
                    if(recursive)
                    {
                        print_recursive(n, file_path, recursive, current_depth+1, max_depth);
                    }
                }
                

                foreach (var file_path in root.EnumerateFiles())
                {
                    tree.AddNode(Cli.ToMarkup($"[red]{file_path.Name}[/]"));
                }
            }
        });
    }
}
