using System.IO;
using Workbench.Shared.Extensions;

namespace Workbench.Shared;

public static class Which
{
    // todo(Gustav): test this

    public static Found<Fil> FindPaths(Vfs vfs, string name)
    {
        var executable = Core.IsWindows() && Path.GetExtension(name) != ".exe"
            ? Path.ChangeExtension(name.Trim(), "exe")
            : name.Trim()
            ;
        return GetPaths()
            .Select<Dir, FoundEntry<Fil>>(dir =>
            {
                var file = dir.GetFile(executable);
                if (file.Exists(vfs) == false)
                {
                    return new FoundEntry<Fil>.Error($"{file.Name} not found in {dir}");
                }

                if (Core.IsWindows() == false)
                {
                    const UnixFileMode EXECUTE_FLAGS = UnixFileMode.GroupExecute | UnixFileMode.UserExecute | UnixFileMode.OtherExecute;
                    if ( (file.UnixFileMode(vfs) & EXECUTE_FLAGS) == 0)
                    {
                        return new FoundEntry<Fil>.Error($"{file} not marked as executable");
                    }
                }

                return new FoundEntry<Fil>.Result(file);
            })
            .Collect("Paths")
            ;
    }

    public static Found<Fil> FindPaths(Vfs vfs, Func<string, bool> validate_name)
    {
        return GetPaths()
                .SelectMany(dir =>
                {
                    return dir.EnumerateFiles(vfs)
                        .Where(file => validate_name(file.NameWithoutExtension))
                        .Select<Fil, FoundEntry<Fil>>(file => {
                        if (Core.IsWindows() == false)
                        {
                            const UnixFileMode EXECUTE_FLAGS = UnixFileMode.GroupExecute | UnixFileMode.UserExecute | UnixFileMode.OtherExecute;
                            if ((file.UnixFileMode(vfs) & EXECUTE_FLAGS) == 0)
                            {
                                return new FoundEntry<Fil>.Error($"{file} not marked as executable");
                            }
                        }

                        return new FoundEntry<Fil>.Result(file);
                    });
                })
                .Collect("Extra paths")
            ;
    }

    private static IEnumerable<Dir> GetPaths()
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
            .Select(x => new Dir(x))
            ;
    }
}
