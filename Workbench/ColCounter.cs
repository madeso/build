namespace Workbench;

public class ColCounter<T>
    where T : notnull
{
    readonly Dictionary<T, int> data = new();

    public void Add(T key, int count)
    {
        if (data.TryGetValue(key, out var value) == false)
        {
            data.Add(key, count);
            return;
        }
        data[key] = value + count;
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

    public void update(ColCounter<T> rhs)
    {
        foreach (var (key, count) in rhs.data)
        {
            Add(key, count);
        }
    }

    public IEnumerable<T> Keys
    {
        get
        {
            return data.Keys;
        }
    }
}
