using Workbench.Shared;

namespace Test;

internal class VfsReadTest : VfsRead
{
    private class Entry
    {
        public readonly Dictionary<string, Entry> directories = new();
        public readonly List<Fil> files = new();
    }

    private readonly Dictionary<string, Entry> directories = new();
    private readonly Dictionary<string, string> files = new();

    private Entry GetEntry(Dir dir)
    {
        if (directories.TryGetValue(dir.Path, out var entry))
        { return entry; }

        entry = new Entry();
        directories[dir.Path] = entry;

        var parent = dir.Parent;
        if (parent != null)
        {
            GetEntry(parent).directories.Add(dir.Name, entry);
        }

        return entry;
    }

    public bool Exists(Fil file_info)
    {
        return files.ContainsKey(file_info.Path);
    }

    public Task<string> ReadAllTextAsync(Fil full_name)
    {
        return Task<string>.Factory.StartNew(() => files[full_name.Path]);
    }

    public void AddContent(Fil file, string content)
    {
        var dir = file.Directory;
        if (dir != null)
        {
            GetEntry(dir).files.Add(file);
        }

        files.Add(file.Path, content);
    }

    public IEnumerable<Fil> GetFiles(Dir dir)
    {
        return GetEntry(dir).files;
    }

    public IEnumerable<Dir> GetDirectories(Dir root)
    {
        return GetEntry(root).directories.Keys.Select(root.GetDir);
    }

    public IEnumerable<Fil> GetFilesRec(Dir dir)
    {
        return RecurseFiles(GetEntry(dir));

        static IEnumerable<Fil> RecurseFiles(Entry entry)
        {
            foreach (var d in entry.directories.Values)
            {
                foreach (var f in RecurseFiles(d))
                {
                    yield return f;
                }
            }
            foreach (var f in entry.files)
            {
                yield return f;
            }
        }
    }
}

internal class VfsWriteTest : VfsWrite
{
    private readonly Dictionary<string, string> files = new();

    public Task WriteAllTextAsync(Fil path, string contents)
    {
        return Task.Factory.StartNew(() => { files.Add(path.Path, contents); });
    }

    public string GetContent(Fil file)
    {
        if (files.Remove(file.Path, out var content))
        {
            return content;
        }
        else
        {
            throw new FileNotFoundException($"{file.Path} was not added to container.\n{GetRemainingFilesAsText()}");
        }
    }

    public IEnumerable<string> GetLines(Fil file) => GetContent(file).Split('\n', StringSplitOptions.TrimEntries).Where(s => string.IsNullOrWhiteSpace(s) == false);

    public IEnumerable<string> RemainingFiles => files.Keys;

    internal bool IsEmpty()
    {
        return files.Count == 0;
    }

    internal string GetRemainingFilesAsText()
    {
        var fs = string.Join(" ", files.Keys);
        return $"files: [{fs}]";
    }
}

internal class RunTest : Run
{
    public List<string> Errors { get; } = new();

    public bool HasError()
    {
        return Errors.Count > 0;
    }

    public void Status(string message)
    {
    }

    public void WriteError(string message)
    {
        Errors.Add(message);
    }

    internal string GetOutput()
    {
        return string.Join("\n", Errors);
    }
}

public class TestBase
{
    internal RunTest run = new();
    internal VfsReadTest read = new();
    internal VfsWriteTest write = new();
}
