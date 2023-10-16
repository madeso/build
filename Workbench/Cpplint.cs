using Workbench.Utils;

namespace Workbench;

public static class Cpplint
{
    public static int HandleList(Printer print, string root)
    {
        var files = FileUtil.ListAllFiles(root);
        foreach(var f in files)
        {
            Printer.Info(f);
        }

        return 0;
    }


    private static void PrintError(Printer printer, string path, string stdout)
    {
        Printer.Line();
        printer.Error(path);
        Printer.Info(stdout);
        Printer.Line();
        Printer.Info("");
    }

    private record LintError(string File, string[] Error);

    static LintError? run_file(Printer printer, string path)
    {
        var ret = new ProcessBuilder("cpplint", path).RunAndGetOutput();
        if(ret.ExitCode != 0)
        {
            PrintError(printer, path, string.Join("\n", ret.Output.Select(x=>x.Line)));
            return new LintError(path, ret.Output.Select(x => x.Line).ToArray());
        }
        else
        {
            Printer.Info(path);
        }
        return null;
    }


    public static int HandleRun(Printer printer, string root)
    {
        var files = FileUtil.ListAllFiles(root);
        var hasErrors = false;
        foreach(var f in files)
        {
            var e = run_file(printer, f);
            if(e != null)
            {
                hasErrors = true;
            }
        }

        if(hasErrors)
        {
            return -1;
        }
        else
        {
            return 0;
        }
    }
}
