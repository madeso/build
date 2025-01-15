using System.Text.Json.Serialization;
using Workbench.Shared;

namespace Workbench.Config;

internal class GitFile
{
    [JsonPropertyName("latest")]
    public string LatestCommit { get; set; } = string.Empty;

    [JsonPropertyName("files")]
    public Dictionary<string, List<Fil>> File { get; set; } = new();

    public static Fil GetPath(Dir cwd)
        => cwd.GetFile(".git-files.cache");
}