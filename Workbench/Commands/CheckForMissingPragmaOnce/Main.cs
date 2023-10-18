using Spectre.Console.Cli;
using Spectre.Console;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Workbench.Shared;

namespace Workbench.Commands.CheckForMissingPragmaOnce;


/*

Previous commands for refactoring info:
wb tools check-missing-pragma-once libs apps
wb tools check-missing-pragma-once libs apps

*/
[Description("find headers with missing include guards")]
internal sealed class CheckForMissingPragmaOnceCommand : Command<CheckForMissingPragmaOnceCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("File to read")]
        [CommandArgument(0, "<input files>")]
        public string[] Files { get; set; } = Array.Empty<string>();
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var count = 0;
        var total_files = 0;

        foreach (var file in FileUtil.ListFilesRecursively(settings.Files, FileUtil.HeaderFiles))
        {
            total_files += 1;
            if (contains_pragma_once(file))
            {
                continue;
            }

            AnsiConsole.WriteLine(file);
            count += 1;
        }

        AnsiConsole.WriteLine($"Found {count} in {total_files} files.");
        return 0;

        static bool contains_pragma_once(string path)
        {
            return File.ReadLines(path)
                    .Any(line => line.Contains("#pragma once"))
                ;
        }
    }
}





public static class Main
{
    public static void Configure(IConfigurator config, string name)
    {
        config.AddCommand<CheckForMissingPragmaOnceCommand>(name);
    }
}
