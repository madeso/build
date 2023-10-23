using System.Text.Json.Serialization;

namespace Workbench.Config;

internal class GitFile
{
    [JsonPropertyName("latest")]
    public string LatestCommit { get; set; } = string.Empty;

    [JsonPropertyName("files")]
    public Dictionary<string, List<string>> File { get; set; } = new();

    public static string GetPath()
    {
        return Path.Join(Environment.CurrentDirectory, ".git-files.cache");
    }
}