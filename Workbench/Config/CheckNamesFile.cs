using System.Text.Json.Serialization;

namespace Workbench.Config;

internal class CheckNamesFile
{
    [JsonPropertyName("accepted_types")]
    public HashSet<string> AcceptedTypes { get; set; } = new();

    [JsonPropertyName("accepted_functions")]
    public HashSet<string> AcceptedFunctions { get; set; } = new();

    [JsonPropertyName("known_function_prefixes")]
    public HashSet<string> KnownFunctionPrefixes { get; set; } = new();

    [JsonPropertyName("known_function_verbs")]
    public HashSet<string> KnownFunctionVerbs { get; set; } = new();

    [JsonPropertyName("bad_function_verbs")]
    public Dictionary<string, string[]> BadFunctionVerbs { get; set; } = new();

    public static string GetBuildDataPath()
    {
        return Path.Join(Environment.CurrentDirectory, FileNames.CheckNames);
    }

    public static CheckNamesFile? LoadFromDirectoryOrNull(Printer print)
    {
        return ConfigFile.LoadOrNull<CheckNamesFile>(print, GetBuildDataPath());
    }
}
