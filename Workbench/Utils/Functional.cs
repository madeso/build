namespace Workbench.Utils;

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

    public static IEnumerable<T> IgnoreNull<T>(this IEnumerable<T?> it)
    {
        foreach (var t in it)
        {
            if (t == null) continue;
            yield return t;
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
                yield return new (left_enumerator.Current, right_enumerator.Current);
            }
            else if (has_left)
            {
                yield return new (left_enumerator.Current, null);
            }
            else if (has_right)
            {
                yield return new (null, right_enumerator.Current);
            }

            has_left = left_enumerator.MoveNext();
            has_right = right_enumerator.MoveNext();
        }
    }
}
