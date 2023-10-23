namespace Workbench.Shared;

public class ActionMapper
{
    private readonly Dictionary<string, Func<Task>> actions = new();

    public void Add(Func<Task> action, params string[] names)
    {
        foreach (var name in names)
        {
            actions[name] = action;
        }
    }

    public async Task<bool> Run(string name)
    {
        if (!actions.TryGetValue(name, out var action))
        {
            return false;
        }

        await action();
        return true;

    }
    public IEnumerable<string> Names => actions.Keys;
}