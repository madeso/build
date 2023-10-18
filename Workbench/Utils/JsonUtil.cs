using System.Text.Json;

namespace Workbench.Utils;

public static class JsonUtil
{
    private static readonly JsonSerializerOptions json_options = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static T? Parse<T>(Printer print, string file, string content)
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
            print.Error($"Unable to parse json {file}: {err.Message}");
            return null;
        }
        catch (NotSupportedException err)
        {
            print.Error($"Unable to parse json {file}: {err.Message}");
            return null;
        }
    }

    internal static string Write<T>(T self)
    {
        return JsonSerializer.Serialize(self, json_options);
    }
}
