using System.Text.Json.Serialization;

namespace Workbench.Config;

internal class CheckIncludesFile
{
    [JsonPropertyName("includes")]
    public List<List<string>> IncludeDirectories { get; set; } = new();

    public static string GetBuildDataPath()
    {
        return Path.Join(Environment.CurrentDirectory, FileNames.CheckIncludes);
    }
}
