using System.Text.Json.Serialization;
using Workbench.Shared;

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

    [JsonPropertyName("ignored_files")]
    public HashSet<string> IgnoredFiles { get; set; } = new();

    // todo(Gustav): rename
    public static Fil GetBuildDataPath(Dir cwd)
        => cwd.GetFile(FileNames.CheckNames);

    public static CheckNamesFile? LoadFromDirectoryOrNull(Dir cwd, Log print)
    {
        return ConfigFile.LoadOrNull<CheckNamesFile>(print, GetBuildDataPath(cwd));
    }
}
