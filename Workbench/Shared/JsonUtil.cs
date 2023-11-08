using System.Text.Json;
using Workbench.Config;

namespace Workbench.Shared;

public static class JsonUtil
{
    private static readonly JsonSerializerOptions json_options = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters =
        {
            new FilJsonConverter(),
            new DirJsonConverter(),
            new RegexEntryJsonConverter()
        }
    };

    public static T? Parse<T>(Log? print, Fil file, string content)
        where T : class
    {
        try
        {
            var loaded = JsonSerializer.Deserialize<T>(content, json_options);
            if (loaded == null) { throw new Exception("internal error"); }
            return loaded;
        }
        catch (JsonException err)
        {
            print?.Error($"Unable to parse json {file}: {err.Message}");
            return null;
        }
        catch (NotSupportedException err)
        {
            print?.Error($"Unable to parse json {file}: {err.Message}");
            return null;
        }
    }

    public static T? GetOrNull<T>(Fil path, Log log)
        where T: class
    {
        if (!path.Exists)
        {
            return null;
        }

        var content = path.ReadAllText();
        return Parse<T>(log, path, content);
    }

    internal static string Write<T>(T self)
    {
        return JsonSerializer.Serialize(self, json_options);
    }

    internal static void Save<T>(Fil path, T data)
    {
        path.WriteAllText(Write(data));
    }
}
