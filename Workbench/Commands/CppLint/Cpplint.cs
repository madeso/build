using Spectre.Console;
using Workbench.Shared;
using Workbench.Shared.Extensions;

namespace Workbench.Commands.CppLint;

public static class Cpplint
{
    private static IEnumerable<string> ListAllFiles(string root)
        => FileUtil.FilesInPitchfork(new DirectoryInfo(root), include_hidden: false)
            .Where(file => file.HasAnyExtension(FileUtil.HeaderAndSourceFiles))
            // ignore pch
            .Where(file => file.Name.StartsWith("pch.") == false)
            .Select(f => f.FullName);

    public static int HandleList(Log print, string root)
    {
        var files = ListAllFiles(root);
        foreach (var f in files)
        {
            AnsiConsole.WriteLine(f);
        }

        return 0;
    }


    public static async Task<int> HandleRun(Log log, string root)
    {
        var files = ListAllFiles(root);
        var has_errors = false;
        foreach (var f in files)
        {
            var ret = await new ProcessBuilder("cpplint", f).RunAndGetOutputAsync();
            if (ret.ExitCode != 0)
            {
                var stdout = string.Join("\n", ret.Output.Select(x => x.Line));
                Printer.Line();
                log.Error(f);
                AnsiConsole.WriteLine(stdout);
                Printer.Line();
                AnsiConsole.WriteLine("");
                has_errors = true;
            }
            else
            {
                AnsiConsole.WriteLine(f);
            }
        }

        return has_errors ? -1 : 0;
    }
}
