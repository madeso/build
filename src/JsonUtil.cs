using Newtonsoft.Json;

namespace Workbench;

public static class JsonUtil
{
    public static T? Parse<T>(Printer print, string file, string content)
        where T : class
    {
        try
        {
            var loaded = JsonConvert.DeserializeObject<T>(content);
            if (loaded == null) { throw new Exception("internal error"); }
            return loaded;
        }
        catch (JsonReaderException err)
        {
            print.error($"Unable to parse json {file}: {err.Message}");
            return null;
        }
    }

    internal static string Write<T>(T self)
    {
        return JsonConvert.SerializeObject(self, Formatting.Indented);
    }
}