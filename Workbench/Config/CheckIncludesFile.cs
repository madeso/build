using System.Text.Json;
using System.Text.Json.Serialization;
using Workbench.Shared;

namespace Workbench.Config;

public class RegexEntry
{
    public RegexEntry()
    {
    }

    public RegexEntry(string s)
    {
        Source = s;
    }

    public const int DEFAULT_RANK = 0;

    [JsonPropertyName("rank")]
    public int Rank { get; set; } = DEFAULT_RANK;

    [JsonPropertyName("regex")]
    public string Source { get; set; } = string.Empty;
}

public class RegexEntryJsonConverter : JsonConverter<RegexEntry>
{
    private static JsonSerializerOptions Removed(JsonSerializerOptions src)
    {
        var r = new JsonSerializerOptions(src);
        var found = r.Converters.Where(c => c is RegexEntryJsonConverter).ToArray();
        foreach (var f in found)
        {
            r.Converters.Remove(f);
        }
        return r;
    }
    public override RegexEntry Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
        => reader.TokenType switch
        {
            JsonTokenType.String => new RegexEntry(reader.GetString()!),
            _ => JsonSerializer.Deserialize<RegexEntry>(ref reader, Removed(o))!
        };

    public override void Write(Utf8JsonWriter writer, RegexEntry re, JsonSerializerOptions o)
    {
        if (re.Rank == RegexEntry.DEFAULT_RANK)
        {
            writer.WriteStringValue(re.Source);
        }
        else
        {
            JsonSerializer.Serialize(writer, re, Removed(o));
        }
    }
}

internal class CheckIncludesFile
{
    [JsonPropertyName("includes")]
    public List<List<RegexEntry>> IncludeDirectories { get; set; } = new();

    // todo(Gustav): rename
    public static Fil GetBuildDataPath(Dir cwd)
        => cwd.GetFile(FileNames.CheckIncludes);
}
