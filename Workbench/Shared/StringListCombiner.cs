using System.Text;
using Workbench.Shared.Extensions;

namespace Workbench.Shared;

public class StringListCombiner
{
    public StringListCombiner(string separator, string final_separator, string empty)
    {
        this.separator = separator;
        this.final_separator = final_separator;
        this.empty = empty;
    }


    public StringListCombiner(string separator, string final_separator)
    {
        this.separator = separator;
        this.final_separator = final_separator;
        empty = "";
    }


    public StringListCombiner(string separator)
    {
        this.separator = separator;
        final_separator = separator;
        empty = "";
    }

    public string Combine<T>(IEnumerable<T> arr)
    {
        return Combine(arr.Select(x => x?.ToString()).IgnoreNull());
    }

    public string Combine(IEnumerable<string> arr)
    {
        return CombineArray(arr.ToArray());
    }

    public string CombineArray(string[] strings)
    {
        if (strings.Length == 0) return empty;
        
        StringBuilder builder = new();
        for (var index = 0; index < strings.Length; ++index)
        {
            var value = strings[index];
            builder.Append(value);

            // if this item isn't the last one in the list
            if (strings.Length == index + 1) continue;

            builder.Append(strings.Length == index + 2
                    ? final_separator
                    : separator
                );
        }
        return builder.ToString();
    }

    private readonly string separator;
    private readonly string final_separator;
    private readonly string empty;


    public static StringListCombiner EnglishOr(string empty = "<none>")
    {
        return new StringListCombiner(", ", " or ", empty);
    }

    public static StringListCombiner EnglishAnd(string empty = "<none>")
    {
        return new StringListCombiner(", ", " and ", empty);
    }
}
