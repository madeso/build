using Workbench.Config;
using Workbench.Shared;

namespace Workbench.Commands.Build;

public readonly struct BuildData
{
    public string Name { get; }
    public List<BuildDependency> Dependencies { get; }
    public Dir RootDirectory { get; }
    public Dir BuildDirectory { get; }
    public Dir ProjectDirectory { get; }
    public Dir DependencyDirectory { get; }

    public BuildData(string name, Dir root_dir)
    {
        Name = name;
        Dependencies = new();
        RootDirectory = root_dir;
        BuildDirectory = root_dir.GetDir("build");
        ProjectDirectory = BuildDirectory.GetDir(name);
        DependencyDirectory = BuildDirectory.GetDir("deps");
    }


    // get the path to the settings file
    public Fil GetPathToSettingsFile()
    {
        return BuildDirectory.GetFile(FileNames.BuildSettings);
    }

    public static BuildData? LoadOrNull(Vfs vfs, Dir cwd, Log print)
    {
        return ConfigFile.LoadOrNull<BuildFile, BuildData>(vfs, print, BuildFile.GetBuildDataPath(cwd),
            loaded =>
            {
                var bd = new BuildData(loaded.Name, cwd);
                foreach (var dependency_name in loaded.Dependencies)
                {
                    bd.Dependencies.Add(BuildDependencies.CreateDependency(dependency_name, bd));
                }
                return bd;
            }
        );
    }
}
