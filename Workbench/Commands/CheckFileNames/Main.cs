﻿using Spectre.Console.Cli;
using Spectre.Console;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Workbench.Shared;

namespace Workbench.Commands.CheckFileNames;



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
        return Printer.PrintErrorsAtExit(print => CheckFileNames(print, settings.Files));
    }

    public static int CheckFileNames(Printer print, string[] args_files)
    {
        var files = 0;
        var errors = 0;

        foreach (var file in FileUtil.ListFilesRecursively(args_files, FileUtil.HeaderAndSourceFiles))
        {
            files += 1;
            if (file.Contains('-'))
            {
                errors += 1;
                print.Error($"file name mismatch: {file}");
            }
        }

        AnsiConsole.WriteLine($"Found {errors} errors in {files} files.");

        return errors > 0 ? -1 : 0;
    }
}


public static class Main
{
    public static void Configure(IConfigurator config, string name)
    {
        config.AddCommand<CheckFileNamesCommand>(name);
    }
}
