using Spectre.Console;

namespace Workbench.Shared;

public class ColCounter<T>
    where T : notnull
{
    private readonly Dictionary<T, int> data = new();

    public void Add(T key, int count)
    {
        if (data.TryGetValue(key, out var value) == false)
        {
            data.Add(key, count);
            return;
        }
        Set(key, value + count);
    }

    private void Set(T key, int value)
    {
        data[key] = value;
    }

    public void AddOne(T key)
    {
        Add(key, 1);
    }

    public IEnumerable<(T, int)> MostCommon()
    {
        return data
            .OrderByDescending(x => x.Value)
            .Select(x => (x.Key, x.Value))
            ;
    }

    public int TotalCount()
    {
        return data.Select(x => x.Value).Sum();
    }

    public void Update(ColCounter<T> rhs)
    {
        foreach (var (key, count) in rhs.data)
        {
            Add(key, count);
        }
    }

    internal void Max(ColCounter<T> rhs)
    {
        foreach (var (key, rhs_value) in rhs.data)
        {
            Set(key,
                data.TryGetValue(key, out var self_value)
                    ? Math.Max(self_value, rhs_value)
                    : rhs_value
                );
        }
    }

    public IEnumerable<T> Keys
    {
        get
        {
            return data.Keys;
        }
    }

    public IEnumerable<KeyValuePair<T, int>> Items
    {
        get
        {
            return data;
        }
    }

}


public static class ColCounterExtensions
{
    public static void PrintMostCommon(this ColCounter<string> counter, int most_common_count)
    {
        foreach (var (file, count) in counter.MostCommon().Take(most_common_count))
        {
            AnsiConsole.WriteLine($"{file}: {count}");
        }
    }
}