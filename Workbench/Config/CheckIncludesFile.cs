using System.Text.Json.Serialization;

namespace Workbench.Config;

internal class CheckIncludesFile
{
    [JsonPropertyName("includes")]
    public List<List<string>> IncludeDirectories { get; set; } = new();
}
