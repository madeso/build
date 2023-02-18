namespace Workbench.Utils;

public static class StringExtensions
{
    public static string Repeat(this string text, int count)
    {
        return string.Concat(Enumerable.Repeat(text, count));
    }
}

