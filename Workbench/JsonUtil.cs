using System.Text.Json;

namespace Workbench;

public static class JsonUtil
{
    public static T? Parse<T>(Printer print, string file, string content)
        where T : class
    {
        try
        {
            var loaded = JsonSerializer.Deserialize<T>(content);
            if (loaded == null) { throw new Exception("internal error"); }
            return loaded;
        }
        catch (JsonException err)
        {
            print.error($"Unable to parse json {file}: {err.Message}");
            return null;
        }
        catch (NotSupportedException err)
        {
            print.error($"Unable to parse json {file}: {err.Message}");
            return null;
        }
    }

    internal static string Write<T>(T self)
    {
        return JsonSerializer.Serialize<T>(self, new JsonSerializerOptions
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
    }
}