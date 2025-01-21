using Spectre.Console;
using Workbench.Shared;
using Workbench.Shared.Extensions;

namespace Workbench.Commands.CppLint;

public static class Cpplint
{
    private static IEnumerable<Fil> ListAllFiles(Vfs vfs, Dir root)
        => FileUtil.FilesInPitchfork(vfs, root, include_hidden: false)
            .Where(FileUtil.IsHeaderOrSource)
            // ignore pch
            .Where(file => file.Name.StartsWith("pch.") == false);

    public static int HandleList(Vfs vfs, Dir cwd, Log print, Dir root)
    {
        var files = ListAllFiles(vfs, root);
        foreach (var f in files)
        {
            AnsiConsole.WriteLine(f.GetDisplay(cwd));
        }

        return 0;
    }


    public static async Task<int> HandleRun(Executor exec, Vfs vfs, Config.Paths paths, Dir cwd, Log log, Dir root)
    {
        var cpplint = paths.GetCppLintExecutable(vfs, cwd, log);
        if (cpplint == null)
        {
            return -1;
        }

        var files = ListAllFiles(vfs, root);
        var has_errors = false;
        foreach (var f in files)
        {
            var ret = await new ProcessBuilder(cpplint, f.Path).RunAndGetOutputAsync(exec, cwd);
            if (ret.ExitCode != 0)
            {
                var stdout = string.Join("\n", ret.Output.Select(x => x.Line));
                Printer.Line();
                log.Error(f.GetDisplay(cwd));
                AnsiConsole.WriteLine(stdout);
                Printer.Line();
                AnsiConsole.WriteLine("");
                has_errors = true;
            }
            else
            {
                AnsiConsole.WriteLine(f.GetDisplay(cwd));
            }
        }

        return has_errors ? -1 : 0;
    }
}
