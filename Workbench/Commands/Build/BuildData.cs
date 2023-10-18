using Workbench.Config;
using Workbench.Utils;

namespace Workbench.Commands.Build;

public readonly struct BuildData
{
    public string Name { get; }
    public List<BuildDependency> Dependencies { get; }
    public string RootDirectory { get; }
    public string BuildDirectory { get; }
    public string ProjectDirectory { get; }
    public string DependencyDirectory { get; }

    public BuildData(string name, string root_dir)
    {
        Name = name;
        Dependencies = new();
        RootDirectory = root_dir;
        BuildDirectory = Path.Join(root_dir, "build");
        ProjectDirectory = Path.Join(BuildDirectory, name);
        DependencyDirectory = Path.Join(BuildDirectory, "deps");
    }


    // get the path to the settings file
    public string GetPathToSettingsFile()
    {
        return Path.Join(BuildDirectory, FileNames.BuildSettings);
    }

    public static BuildData? LoadOrNull(Printer print)
    {
        return ConfigFile.LoadOrNull<BuildFile, BuildData>(print, BuildFile.GetBuildDataPath(),
            loaded =>
            {
                var bd = new BuildData(loaded.Name, Environment.CurrentDirectory);
                foreach (var dependency_name in loaded.Dependencies)
                {
                    bd.Dependencies.Add(BuildDependencies.CreateDependency(dependency_name, bd));
                }
                return bd;
            }
        );
    }
}
