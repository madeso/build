using Spectre.Console;
using Workbench.Shared;

namespace Workbench.Commands.CppLint;

public static class Cpplint
{
    public static int HandleList(Printer print, string root)
    {
        var files = FileUtil.ListAllFiles(root);
        foreach (var f in files)
        {
            AnsiConsole.WriteLine(f);
        }

        return 0;
    }


    public static int HandleRun(Printer printer, string root)
    {
        var files = FileUtil.ListAllFiles(root);
        var has_errors = false;
        foreach (var f in files)
        {
            var ret = new ProcessBuilder("cpplint", f).RunAndGetOutput();
            if (ret.ExitCode != 0)
            {
                var stdout = string.Join("\n", ret.Output.Select(x => x.Line));
                Printer.Line();
                printer.Error(f);
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
