using System.Text;

namespace Workbench.Utils;

public class StringListCombiner
{
    public StringListCombiner(string separator, string finalSeparator, string empty)
    {
        _separator = separator;
        _finalSeparator = finalSeparator;
        _empty = empty;
    }


    public StringListCombiner(string separator, string finalSeparator)
    {
        _separator = separator;
        _finalSeparator = finalSeparator;
        _empty = "";
    }


    public StringListCombiner(string separator)
    {
        _separator = separator;
        _finalSeparator = separator;
        _empty = "";
    }


    public static StringListCombiner EnglishOr(string empty = "<none>")
    {
        return new StringListCombiner(", ", " or ", empty);
    }


    public string Combine(string[] strings)
    {
        if (strings.Length == 0) return _empty;
        
        StringBuilder builder = new();
        for (var index = 0; index < strings.Length; ++index)
        {
            var value = strings[index];
            builder.Append(value);

            // if this item isn't the last one in the list
            if (strings.Length == index + 1) continue;

            builder.Append(strings.Length == index + 2
                    ? _finalSeparator
                    : _separator
                );
        }
        return builder.ToString();
    }

    private readonly string _separator;
    private readonly string _finalSeparator;
    private readonly string _empty;
}
