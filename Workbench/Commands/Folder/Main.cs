using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;
using Workbench.Shared;

namespace Workbench.Commands.Folder;

public class Main
{
    public static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, root =>
        {
            root.SetDescription("Folder tools");
            
            root.AddCommand<ShowHiddenCommand>("show-hidden").WithDescription("Show hidden files");
            root.AddCommand<RemoveEmptyCommand>("remove-empty").WithDescription("Remove empty directories in a tree");
        });
    }
}

internal sealed class ShowHiddenCommand : Command<ShowHiddenCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Directory to show hidden files in")]
        [CommandArgument(2, "[dir]")]
        public string Directory { get; init; } = string.Empty;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        return Log.PrintErrorsAtExit(log =>
        {
            var dir = Cli.RequireDirectory(log, arg.Directory, "directory");
            if (dir == null)
            {
                return -1;
            }

            FolderTool.Start(dir, FolderTool.Type.Show);
            return 0;
        });
    }
}

internal sealed class RemoveEmptyCommand : Command<RemoveEmptyCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Directory to recursivly remove if empty")]
        [CommandArgument(2, "[dir]")]
        public string Directory { get; init; } = string.Empty;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        return Log.PrintErrorsAtExit(log =>
        {
            var dir = Cli.RequireDirectory(log, arg.Directory, "directory");
            if (dir == null)
            {
                return -1;
            }


            FolderTool.Start(dir, FolderTool.Type.Remove);
            return 0;
        });
    }
}

public class FolderTool
{
    public enum Type { Remove, Show }

    public static void Start(Dir dir, Type type)
    {
        AnsiConsole.WriteLine($"Started on {dir}");

        if (type == Type.Remove)
        {
            RemoveDirectoriesRec(dir);
        }
        else
        {
            ShowHiddenFiles(dir);
        }

        AnsiConsole.WriteLine("done");
    }

    private static void ShowHiddenFiles(Dir dir)
    {
        foreach (var d in dir.EnumerateDirectories())
        {
            ShowHiddenFiles(d);
        }

        foreach (var f in dir.EnumerateFiles())
        {
            var fi = new FileInfo(f.Path);
            if ((fi.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
            {
                AnsiConsole.WriteLine($"Setting {f} from {fi.Attributes} to normal");
                fi.Attributes = FileAttributes.Normal;
            }
        }
    }

    private static void RemoveDirectoriesRec(Dir dir)
    {
        if (false == dir.Exists)
        {
            AnsiConsole.WriteLine($"{dir} doesn't exist");
            return;
        }

        var dirs = dir.EnumerateDirectories().ToImmutableArray();
        foreach (var sub in dirs)
        {
            RemoveDirectoriesRec(sub);
        }

        var files = dir.EnumerateFiles().ToImmutableArray();
        if (files.Any())
        {
            var valid_files = new List<Fil>();
            foreach (var f in files)
            {
                if (IsUselessFile(f))
                {
                    AnsiConsole.WriteLine($"Deleting {f}");
                    try
                    {
                        File.Delete(f.Path);
                    }
                    catch (Exception e)
                    {
                        AnsiConsole.WriteLine(e.Message);
                    }
                }
                else
                {
                    if (IsStrangeFile(f))
                    {
                        valid_files.Add(f);
                    }
                }
            }

            if (valid_files.Count < 10)
            {
                foreach (var f in valid_files)
                {
                    AnsiConsole.WriteLine($"   {f}: {new FileInfo(f.Path).Attributes}");
                }
            }
        }

        // contains non-empty subdirectories...
        if (dir.EnumerateDirectories().Any()) return;

        // contains files
        if (dir.EnumerateFiles().Any()) return;

        try
        {
            AnsiConsole.WriteLine($"{dir} removing...");
            var di = new DirectoryInfo(dir.Path);
            di.Attributes = FileAttributes.Normal;

            Directory.Delete(dir.Path);
        }
        catch (Exception e)
        {
            AnsiConsole.WriteLine($"ERROR: {dir} was not removed, {e.Message}");
            return;
        }

        AnsiConsole.WriteLine($"{dir} removed!");
    }

    private static bool IsUselessFile(Fil filename)
    {
        var f = filename.Path.ToLower();
        if (f.EndsWith("thumbs.db")) return true;
        if (f.EndsWith("desktop.ini")) return true;
        return false;
    }

    private static bool IsStrangeFile(Fil filename) =>
        filename.Extension switch
        {
            ".mp3" or
                ".mod" or
                ".ogg" or
                ".sid" => false,
            _ => true
        };
}