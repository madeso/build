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

    public static string GetSafeString(this string str)
    {
        // algorithm inspired by the description of the doxygen version
        // https://stackoverflow.com/a/30490482
        var buf = "";

        foreach (var c in str)
        {
            buf += c switch
            {
                // '0' .. '9'
                '0' or '1' or '2' or '3' or '4' or '5' or '6' or '7' or '8' or '9' or
                    // 'a'..'z'
                    'a' or 'b' or 'c' or 'd' or 'e' or 'f' or 'g' or 'h' or 'i' or 'j' or
                    'k' or 'l' or 'm' or 'n' or 'o' or 'p' or 'q' or 'r' or 's' or 't' or
                    'u' or 'v' or 'w' or 'x' or 'y' or 'z' or
                    // other safe characters...
                    // is _ considered safe? we only care about one way translation
                    // so it should be safe.... right?
                    '-' or '_'
                    => $"{c}",
                // 'A'..'Z'
                // 'A'..'Z'
                'A' or 'B' or 'C' or 'D' or 'E' or 'F' or 'G' or 'H' or 'I' or 'J' or
                    'K' or 'L' or 'M' or 'N' or 'O' or 'P' or 'Q' or 'R' or 'S' or 'T' or
                    'U' or 'V' or 'W' or 'X' or 'Y' or 'Z'
                    => $"_{char.ToLowerInvariant(c)}",
                _ => $"_{(int)c}"
            };
        }

        return buf;
    }
}

