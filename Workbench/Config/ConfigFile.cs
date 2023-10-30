using Spectre.Console;
using Workbench.Shared;
using static Workbench.Shared.Git;

namespace Workbench.Config;

internal static class ConfigFile
{
    public static TFile? LoadOrNull<TFile>(Log? print, Fil file)
        where TFile : class
    {
        if (file.Exists == false)
        {
            print?.Error($"Unable to read file: {file}");
            return null;
        }
        var content = file.ReadAllText();
        var loaded = JsonUtil.Parse<TFile>(print, file, content);
        if (loaded == null)
        {
            print?.Error($"Unable to parse file: {file}");
            return null;
        }

        return loaded;
    }

    public static TData? LoadOrNull<TFile, TData>(Log print, Fil file, Func<TFile, TData> enrich)
        where TData : struct
        where TFile: class
    {
        var loaded = LoadOrNull<TFile>(print, file);
        if (loaded == null)
        {
            return null;
        }

        return enrich(loaded);
    }

    internal static int WriteInit<T>(Log print, bool overwrite, Fil path, T data)
    {
        var content = JsonUtil.Write(data);

        if (overwrite == false && path.Exists)
        {
            print.Error($"{path} already exist and overwrite was not requested");
            return -1;
        }

        path.WriteAllText(content);
        AnsiConsole.WriteLine($"Wrote {path}");
        return 0;
    }

    internal static int Write<T>(Fil path, T data)
    {
        var content = JsonUtil.Write(data);

        path.WriteAllText(content);
        AnsiConsole.WriteLine($"Wrote {path}");
        return 0;
    }
}
