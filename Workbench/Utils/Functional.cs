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
        using var leftEnumerator = left.GetEnumerator();
        using var rightEnumerator = right.GetEnumerator();

        var hasLeft = leftEnumerator.MoveNext();
        var hasRight = rightEnumerator.MoveNext();

        while (hasLeft || hasRight)
        {
            if (hasLeft && hasRight)
            {
                yield return new (leftEnumerator.Current, rightEnumerator.Current);
            }
            else if (hasLeft)
            {
                yield return new (leftEnumerator.Current, null);
            }
            else if (hasRight)
            {
                yield return new (null, rightEnumerator.Current);
            }

            hasLeft = leftEnumerator.MoveNext();
            hasRight = rightEnumerator.MoveNext();
        }
    }
}
