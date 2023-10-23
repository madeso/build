using System.Collections.Generic;
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

    public static async IAsyncEnumerable<TT> SelectAsync<T, TT>(this IAsyncEnumerable<T> tt, Func<T, TT> sel)
    {
        await foreach (var t in tt)
        {
            yield return sel(t);
        }
    }
}