using System.Text.Json.Serialization;
using Workbench.Shared;

namespace Workbench.Config;

internal class CheckIncludesFile
{
    [JsonPropertyName("includes")]
    public List<List<string>> IncludeDirectories { get; set; } = new();

    // todo(Gustav): rename
    public static Fil GetBuildDataPath()
        => Dir.CurrentDirectory.GetFile(FileNames.CheckIncludes);
}
