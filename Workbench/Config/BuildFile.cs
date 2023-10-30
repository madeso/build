using System.Text.Json.Serialization;
using Workbench.Commands.Build;
using Workbench.Shared;

namespace Workbench.Config;

internal class BuildFile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("dependencies")]
    public List<DependencyName> Dependencies { get; set; } = new();


    // todo(Gustav): rename
    public static Fil GetBuildDataPath()
        => Dir.CurrentDirectory.GetFile(FileNames.BuildData);
}
