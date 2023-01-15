namespace Workbench.Config;

internal static class ConfigFile
{
    public static TData? LoadOrNull<TFile, TData>(Printer print, string file, Func<TFile, TData> enrich)
        where TData : struct
        where TFile: class
    {
        if (File.Exists(file) == false)
        {
            print.Error($"Unable to read file: {file}");
            return null;
        }
        var content = File.ReadAllText(file);
        var loaded = JsonUtil.Parse<TFile>(print, file, content);
        if (loaded == null)
        {
            return null;
        }

        return enrich(loaded);
    }

    internal static int WriteInit<T>(Printer print, bool overwrite, string path, T data)
    {
        var content = JsonUtil.Write(data);

        if (overwrite == false && File.Exists(path))
        {
            print.Error($"{path} already exist and overwrite was not requested");
            return -1;
        }

        File.WriteAllText(path, content);
        print.Info($"Wrote {path}");
        return 0;
    }
}
