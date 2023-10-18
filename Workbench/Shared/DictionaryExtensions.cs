using System.Collections.Immutable;

namespace Workbench.Shared;

public static class DictionaryExtensions
{
    public static void RemoveAll<TKey, TValue>(this IDictionary<TKey, TValue> dict,
        Func<KeyValuePair<TKey, TValue>, bool> predicate)
    {
        var keys = dict
            .Where(predicate)
            .Select(k => k.Key)
            .ToImmutableArray();

        foreach (var key in keys)
        {
            dict.Remove(key);
        }
    }
}
