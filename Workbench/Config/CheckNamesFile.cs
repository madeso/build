using System.Text.Json.Serialization;

namespace Workbench.Config;

internal class CheckNamesFile
{
    [JsonPropertyName("types")]
    public HashSet<string> Types { get; set; } = new();

    [JsonPropertyName("functions")]
    public HashSet<string> Functions { get; set; } = new();

    public static string GetBuildDataPath()
    {
        return Path.Join(Environment.CurrentDirectory, "names.wb.json");
    }

    public static CheckNamesFile? LoadFromDirectoryOrNull(Printer print)
    {
        return ConfigFile.LoadOrNull<CheckNamesFile>(print, GetBuildDataPath());
    }
}
