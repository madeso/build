namespace Workbench;

public static class Which
{
    // todo(Gustav): test this

    public static string? Find(string binaryArg)
    {
        var binary = Core.is_windows() && Path.GetExtension(binaryArg) != ".exe"
            ? Path.ChangeExtension(binaryArg.Trim(), "exe")
            : binaryArg.Trim()
            ;
        foreach (var p in GetPaths())
        {
            var ret = Path.Join(p, binary);
            if (File.Exists(ret)) return ret;
        }

        return null;
    }

    private static IEnumerable<string> GetPaths()
    {
        var targets = new EnvironmentVariableTarget[]
        {
            EnvironmentVariableTarget.Process,
            EnvironmentVariableTarget.User,
            EnvironmentVariableTarget.Machine
        };
        foreach (var target in targets)
        {
            var path = Environment.GetEnvironmentVariable("PATH", target);
            if (path == null) { continue; }
            var paths = path.Split(';', StringSplitOptions.TrimEntries);
            foreach (var p in paths) { yield return p; }
        }
    }
}
