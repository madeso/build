using System.Collections.Immutable;

namespace Workbench.Utils;

public static class DictionaryExtensions
{
    public static void RemoveAll<TKey, TValue>(this IDictionary<TKey, TValue> dict, Func<KeyValuePair<TKey, TValue>, bool> predicate)
    {
        var keys = dict
            .Where(k => predicate(k))
            .Select(k => k.Key)
            .ToImmutableArray();

        foreach (var key in keys)
        {
            dict.Remove(key);
        }
    }
}
