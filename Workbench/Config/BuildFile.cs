using System.Text.Json.Serialization;
using Workbench.Commands.Build;

namespace Workbench.Config;

internal class BuildFile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("dependencies")]
    public List<DependencyName> Dependencies { get; set; } = new();

    public static string GetBuildDataPath()
    {
        return Path.Join(Environment.CurrentDirectory, FileNames.BuildData);
    }
}
