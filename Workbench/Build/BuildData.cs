namespace Workbench;


using System.IO;
using System.Text.Json.Serialization;



public struct BuildData
{
    public string Name { get; }
    public List<Dependency> Dependencies { get; }
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
        return Path.Join(BuildDirectory, "settings.json");
    }

    public static BuildData? LoadFromDirectoryOrNull(string root, Printer print)
    {
        string file = GetBuildDataPath(root);
        if (File.Exists(file) == false)
        {
            print.Error($"Unable to read file: {file}");
            return null;
        }
        var content = File.ReadAllText(file);
        var loaded = JsonUtil.Parse<Config.BuildFile>(print, file, content);
        if (loaded == null)
        {
            return null;
        }

        var bd = new BuildData(loaded.Name, root, print);
        foreach (var dependency_name in loaded.Dependencies)
        {
            bd.Dependencies.Add(Workbench.Dependencies.CreateDependency(dependency_name, bd));
        }
        return bd;
    }

    public static string GetBuildDataPath(string root)
    {
        return Path.Join(root, "project.wb.json");
    }

    public static BuildData? LoadOrNull(Printer print)
    {
        return LoadFromDirectoryOrNull(Environment.CurrentDirectory, print);
    }

}