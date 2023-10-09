using Workbench.Config;

namespace Workbench.Build;

public readonly struct BuildData
{
    public string Name { get; }
    public List<BuildDependency> Dependencies { get; }
    public string RootDirectory { get; }
    public string BuildDirectory { get; }
    public string ProjectDirectory { get; }
    public string DependencyDirectory { get; }

    public BuildData(string name, string rootDir, Printer print)
    {
        Name = name;
        Dependencies = new();
        RootDirectory = rootDir;
        BuildDirectory = Path.Join(rootDir, "build");
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
        return ConfigFile.LoadOrNull<BuildFile, BuildData>(print, Config.BuildFile.GetBuildDataPath(),
            loaded =>
            {
                var bd = new BuildData(loaded.Name, Environment.CurrentDirectory, print);
                foreach (var dependencyName in loaded.Dependencies)
                {
                    bd.Dependencies.Add(BuildDependencies.CreateDependency(dependencyName, bd));
                }
                return bd;
            }
        );
    }
}
