namespace Workbench;

public class Found
{
    public string? value { get; }
    public string name { get; }

    public Found(string? value, string name)
    {
        this.value = value;
        this.name = name;
    }

    public override string ToString()
    {
        if (value != null)
        {
            return $"Found {value} from {name}";
        }
        else
        {
            return $"NOT FOUND in {name}";
        }
    }

    public static string? GetFirstValueOrNull(IEnumerable<Found> founds)
    {
        return founds
            .Where(found => found.value != null)
            .Select(found => found.value)
            .FirstOrDefault();
    }
}
