using System.Text;

namespace Workbench;

public class StringListCombiner
{
    public StringListCombiner(string seperator, string finalSeperator, string empty)
    {
        this.seperator = seperator;
        this.finalSeperator = finalSeperator;
        this.empty = empty;
    }
    public StringListCombiner(string seperator, string finalSeperator)
    {
        this.seperator = seperator;
        this.finalSeperator = finalSeperator;
        empty = "";
    }
    public StringListCombiner(string seperator)
    {
        this.seperator = seperator;
        finalSeperator = seperator;
        empty = "";
    }

    public static StringListCombiner EnglishOr(string empty = "<none>")
    {
        return new StringListCombiner(", ", " or ", empty);
    }

    public string combine(string[] strings)
    {
        if (strings.Length == 0) return empty;
        StringBuilder builder = new();
        for (int index = 0; index < strings.Length; ++index)
        {
            string value = strings[index];
            builder.Append(value);

            if (strings.Length != index + 1) // if this item isnt the last one in the list
            {
                string s = seperator;
                if (strings.Length == index + 2)
                {
                    s = finalSeperator;
                }
                builder.Append(s);
            }
        }
        return builder.ToString();
    }

    private readonly string seperator;
    private readonly string finalSeperator;
    private readonly string empty;
}
