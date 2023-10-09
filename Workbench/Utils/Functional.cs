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
}
