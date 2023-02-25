using System.Text.Json.Serialization;

namespace Workbench.Config;

internal class CheckNamesFile
{
    [JsonPropertyName("accepted_types")]
    public HashSet<string> AcceptedTypes { get; set; } = new();

    [JsonPropertyName("accepted_functions")]
    public HashSet<string> AcceptedFunctions { get; set; } = new();

    public static string GetBuildDataPath()
    {
        return Path.Join(Environment.CurrentDirectory, FileNames.CheckNames);
    }

    public static CheckNamesFile? LoadFromDirectoryOrNull(Printer print)
    {
        return ConfigFile.LoadOrNull<CheckNamesFile>(print, GetBuildDataPath());
    }
}
