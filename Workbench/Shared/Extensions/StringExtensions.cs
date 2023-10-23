namespace Workbench.Shared.Extensions;

public static class StringExtensions
{
    public static string Repeat(this string text, int count)
    {
        return string.Concat(Enumerable.Repeat(text, count));
    }

    public static string? NullIfEmpty(this string? s)
    {
        return string.IsNullOrEmpty(s) ? null : s;
    }
}

