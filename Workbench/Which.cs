using Workbench.Utils;

namespace Workbench;

public static class Which
{
    // todo(Gustav): test this

    public static string? Find(string binaryArg)
    {
        var binary = Core.IsWindows() && Path.GetExtension(binaryArg) != ".exe"
            ? Path.ChangeExtension(binaryArg.Trim(), "exe")
            : binaryArg.Trim()
            ;
        return GetPaths()
            .Select(p => Path.Join(p, binary))
            .FirstOrDefault(File.Exists)
            ;
    }

    private static IEnumerable<string> GetPaths()
    {
        var targets = new[]
        {
            EnvironmentVariableTarget.Process,
            EnvironmentVariableTarget.User,
            EnvironmentVariableTarget.Machine
        };

        return targets
            .Select(target => Environment.GetEnvironmentVariable("PATH", target))
            .IgnoreNull()
            .SelectMany(path => path.Split(';', StringSplitOptions.TrimEntries))
            ;
    }
}
