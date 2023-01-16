namespace Workbench;

public static class Cpplint
{
    static IEnumerable<string> list_files_in_dir(string dir)
    {
        return FileUtil.ListFilesRecursivly(dir, FileUtil.HEADER_AND_SOURCE_FILES)
            .Where(x => new FileInfo(x).Name.StartsWith("pch.") == false);
    }


    static IEnumerable<string> list_all_files(string root)
    {
        IEnumerable<string> Files(string relativeDir)
        {
            var dir = new DirectoryInfo(Path.Join(root, relativeDir)).FullName;
            if (Directory.Exists(dir) == false)
            {
                yield break;
            }
            foreach(var f in list_files_in_dir(dir))
            {
                yield return f;
            }    
        }

        return FileUtil.PITCHFORK_FOLDERS
            .Select(Files)
            .Aggregate((a, b) => a.Concat(b));
    }


    public static int HandleList(Printer print, string root)
    {
        var files = list_all_files(root);
        foreach(var f in files)
        {
            print.Info(f);
        }

        return 0;
    }


    static void print_err(Printer printer, string path, string stdout)
    {
        printer.Line();
        printer.Error(path);
        printer.Info(stdout);
        printer.Line();
        printer.Info("");
    }

    record LintError(string File, string[] Error);

    static LintError? run_file(Printer printer, string path)
    {
        var ret = new ProcessBuilder("cpplint", path).RunAndGetOutput();
        if(ret.ExitCode != 0)
        {
            print_err(printer, path, string.Join("\n", ret.Output));
            return new LintError(path, ret.Output);
        }
        else
        {
            printer.Info(path);
        }
        return null;
    }


    public static int HandleRun(Printer printer, string root)
    {
        var files = list_all_files(root);
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