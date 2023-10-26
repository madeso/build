using System.Reflection.PortableExecutable;
using System.Text.Json;
using Workbench.Commands.Build;

namespace Workbench.Shared;

public static class JsonUtil
{
    private static readonly JsonSerializerOptions json_options = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static T? Parse<T>(Log? print, string file, string content)
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

    public static T? GetOrNull<T>(string path, Log log)
        where T: class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var content = File.ReadAllText(path);
        return Parse<T>(log, path, content);
    }

    internal static string Write<T>(T self)
    {
        return JsonSerializer.Serialize(self, json_options);
    }

    internal static void Save<T>(string path, T data)
    {
        File.WriteAllText(path, Write(data));
    }
}
