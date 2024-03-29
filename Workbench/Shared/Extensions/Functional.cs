using System.Collections.Immutable;

namespace Workbench.Shared.Extensions;

internal static class Functional
{
    // start is returned first
    public static IEnumerable<int> Integers(int start = 0, int step = 1)
    {
        while (true)
        {
            yield return start;
            start += step;
        }
    }

    public static T[] Params<T>(params T[] args)
    {
        return args;
    }

    public static IEnumerable<T> IgnoreNull<T>(this IEnumerable<T?> it)
    {
        foreach (var t in it)
        {
            if (t == null) continue;
            yield return t;
        }
    }

    public static IEnumerable<TDst> SelectNonNull<TSrc, TDst>(
        this IEnumerable<TSrc> it,
        Func<TSrc, TDst?> sel, Action<TSrc> fail)
    {
        foreach (var t in it)
        {
            var r = sel(t);
            if (r == null)
            {
                fail(t);
                continue;
            }
            yield return r;
        }
    }

    public static IEnumerable<T> Where<T>(this IEnumerable<T> src, Func<T, bool> predicate, Action<T> fail)
    {
        foreach (var t in src)
        {
            if (predicate(t))
            {
                yield return t;
            }
            else
            {
                fail(t);
            }
        }
    }

    public static IEnumerable<(T?, T?)> ZipLongest<T>(this IEnumerable<T> left, IEnumerable<T> right)
        where T : class
    {
        using var left_enumerator = left.GetEnumerator();
        using var right_enumerator = right.GetEnumerator();

        var has_left = left_enumerator.MoveNext();
        var has_right = right_enumerator.MoveNext();

        while (has_left || has_right)
        {
            if (has_left && has_right)
            {
                yield return new(left_enumerator.Current, right_enumerator.Current);
            }
            else if (has_left)
            {
                yield return new(left_enumerator.Current, null);
            }
            else if (has_right)
            {
                yield return new(null, right_enumerator.Current);
            }

            has_left = left_enumerator.MoveNext();
            has_right = right_enumerator.MoveNext();
        }
    }

    public static IEnumerable<(T, T)> Permutation<T>(this IEnumerable<T> iter, bool reverse = true)
    {
        var c = iter.ToImmutableArray();

        for (var i = 0; i < c.Length; i++)
        {
            for (var j = 0; j < c.Length; j++)
            {
                if (i == j) continue;
                yield return (c[i], c[j]);
                if (reverse)
                {
                    yield return (c[j], c[i]);
                }
            }
        }
    }
}
