namespace Workbench.Shared;

// todo(Gustav): expand to include more than one?
// either we want
// * the first valid one (including commandline arg) or
// * make sure there is a single in a list of many (or override with commandline arg)
// and the current design only caters to the first one
public record Found(string? Value, string Name)
{
    public override string ToString()
    {
        return Value != null
            ? $"Found {Value} from {Name}"
            : $"NOT FOUND in {Name}"
            ;
    }
}

public static class FoundExtensions
{
    public static string? GetFirstValueOrNull(this IEnumerable<Found> founds)
    {
        return founds
            .Where(found => found.Value != null)
            .Select(found => found.Value)
            .FirstOrDefault();
    }
}