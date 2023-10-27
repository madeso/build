using System.IO;
using Workbench.Shared.Extensions;

namespace Workbench.Shared;

public static class Which
{
    // todo(Gustav): test this

    public static Found<string> FindPaths(string name)
    {
        var executable = Core.IsWindows() && Path.GetExtension(name) != ".exe"
            ? Path.ChangeExtension(name.Trim(), "exe")
            : name.Trim()
            ;
        return GetPaths()
            .Select<string, FoundEntry<string>>(path =>
            {
                var file = new FileInfo(Path.Join(path, executable));
                if (file.Exists == false)
                {
                    return new FoundEntry<string>.Error($"{executable} not found in {path}");
                }

                if (Core.IsWindows() == false)
                {
                    const UnixFileMode EXECUTE_FLAGS = UnixFileMode.GroupExecute | UnixFileMode.UserExecute | UnixFileMode.OtherExecute;
                    if ( (file.UnixFileMode & EXECUTE_FLAGS) == 0)
                    {
                        return new FoundEntry<string>.Error($"{file.FullName} not marked as executable");
                    }
                }

                return new FoundEntry<string>.Result(file.FullName);
            })
            .Collect("Paths")
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
            .SelectMany(path => path.Split(Core.IsWindows() ? ';' : ':', StringSplitOptions.TrimEntries))
            .Where(s => s.Length > 0)
            ;
    }
}
