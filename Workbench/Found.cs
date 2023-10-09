namespace Workbench;

public record Found(string? Value, string Name)
{
    public override string ToString()
    {
        return Value != null
            ? $"Found {Value} from {Name}"
            : $"NOT FOUND in {Name}"
            ;
    }

    public static string? GetFirstValueOrNull(IEnumerable<Found> founds)
    {
        return founds
            .Where(found => found.Value != null)
            .Select(found => found.Value)
            .FirstOrDefault();
    }
}
