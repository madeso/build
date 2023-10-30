using Spectre.Console;
using Workbench.Shared;
using Workbench.Shared.Extensions;

namespace Workbench.Commands.CppLint;

public static class Cpplint
{
    private static IEnumerable<Fil> ListAllFiles(Dir root)
        => FileUtil.FilesInPitchfork(root, include_hidden: false)
            .Where(FileUtil.IsHeaderOrSource)
            // ignore pch
            .Where(file => file.Name.StartsWith("pch.") == false);

    public static int HandleList(Log print, Dir root)
    {
        var files = ListAllFiles(root);
        foreach (var f in files)
        {
            AnsiConsole.WriteLine(f.GetDisplay());
        }

        return 0;
    }


    public static async Task<int> HandleRun(Log log, Dir root)
    {
        var cpplint = Config.Paths.GetCpplintFormatExecutable(log);
        if (cpplint == null)
        {
            return -1;
        }

        var files = ListAllFiles(root);
        var has_errors = false;
        foreach (var f in files)
        {
            var ret = await new ProcessBuilder(cpplint, f.Path).RunAndGetOutputAsync();
            if (ret.ExitCode != 0)
            {
                var stdout = string.Join("\n", ret.Output.Select(x => x.Line));
                Printer.Line();
                log.Error(f.GetDisplay());
                AnsiConsole.WriteLine(stdout);
                Printer.Line();
                AnsiConsole.WriteLine("");
                has_errors = true;
            }
            else
            {
                AnsiConsole.WriteLine(f.GetDisplay());
            }
        }

        return has_errors ? -1 : 0;
    }
}
