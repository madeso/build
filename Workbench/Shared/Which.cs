using Workbench.Shared.Extensions;

namespace Workbench.Shared;

public static class Which
{
    // todo(Gustav): test this

    public static string? FirstValidPath(string executable)
    {
        var binary = Core.IsWindows() && Path.GetExtension(executable) != ".exe"
            ? Path.ChangeExtension(executable.Trim(), "exe")
            : executable.Trim()
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
