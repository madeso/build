using System.Collections.Immutable;

namespace Workbench.Shared.Extensions;

public static class AsyncExtensions
{
    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> enumerable)
    {
        if (null == enumerable)
            throw new ArgumentNullException(nameof(enumerable));

        var list = new List<T>();
        await foreach (var t in enumerable)
        {
            list.Add(t);
        }

        return list;
    }

    public static async Task<List<ImmutableArray<T>>> ToListAsync<T>(this IEnumerable<Task<ImmutableArray<T>>> enumerable)
    {
        if (null == enumerable)
            throw new ArgumentNullException(nameof(enumerable));

        var list = new List<ImmutableArray<T>>();
        foreach (var t in enumerable)
        {
            list.Add(await t);
        }

        return list;
    }
}