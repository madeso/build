using static Workbench.CheckIncludes.RegexOrErr;

namespace Workbench.Utils;

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

    public void update(ColCounter<T> rhs)
    {
        foreach (var (key, count) in rhs.data)
        {
            Add(key, count);
        }
    }

    internal void Max(ColCounter<T> rhs)
    {
        foreach (var (key, rhsValue) in rhs.data)
        {
            if (data.TryGetValue(key, out var selfValue))
            {
                Set(key, Math.Max(selfValue, rhsValue));
            }
            else
            {
                Set(key, rhsValue);
            }
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
