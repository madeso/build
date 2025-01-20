using System.IO;
using Workbench.Config;
using Workbench.Shared;

namespace Test;

internal class FakePath(SavedPaths saved_paths) : Workbench.Config.Paths
{

    public override Found<Fil> Find(Vfs _, Dir cwd, Log? log, Func<SavedPaths, Fil?> getter)
    {
        var r = getter(saved_paths);
        if (r == null) return Found<Fil>.Fail("missing in test", "file");
        else return Found<Fil>.Success(r, "file");
    }

    public override IEnumerable<Found<Fil>> ListAllExecutables(Vfs _, Dir cwd, Func<SavedPaths, Fil?> getter, Executable exe, Log? log = null)
    {
        throw new NotImplementedException();
    }

    public override Fil? GetSavedOrSearchForExecutable(Vfs _, Dir cwd, Log? log, Func<SavedPaths, Fil?> getter, Executable exe)
    {
        return getter(saved_paths);
    }
}

internal class VfsTest : Vfs
{
    class Entry
    {
        public Dictionary<string, string> Files { get; } = new();
        public Dictionary<string, Entry> Dirs { get; } = new();
    }
    private Entry root = new Entry();

    private static string[] SplitPath(string s)
        => s.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

    private static (string[], string) Split(Fil f)
    {
        var split = SplitPath(f.Path);
        var file = split[^1];
        var dirs = split.Take(split.Length - 1).ToArray();
        return (dirs, file);
    }

    private static string[] Split(Dir f)
        => SplitPath(f.Path);

    private Entry? GetDir(string[] entries)
    {
        var r = root;
        foreach (var entry in entries)
        {
            if (r.Dirs.TryGetValue(entry, out var next))
            {
                r = next;
            }
            else
            {
                return null;
            }
        }

        return r;
    }

    private Entry RequireDir(string[] entries)
    {
        var r = root;
        foreach (var entry in entries)
        {
            if (r.Dirs.TryGetValue(entry, out var next))
            {
                r = next;
            }
            else
            {
                throw new Exception("missing dir " + entry);
            }
        }

        return r;
    }


    public string ReadAllText(Fil fil)
    {
        var (dirs, file) = Split(fil);
        var di = RequireDir(dirs);
        if (di.Files.TryGetValue(file, out var value)) return value;
        throw new Exception("missing file" + file);
    }

    public IEnumerable<string> ReadAllLines(Fil fil)
    {
        var t = ReadAllText(fil);
        return t.Split('\n');
    }

    public Task<string[]> ReadAllLinesAsync(Fil fil)
    {
        throw new NotImplementedException();
    }

    public void WriteAllText(Fil fil, string content)
    {
        var (dirs, file) = Split(fil);
        var di = RequireDir(dirs);
        di.Files[file] = content;
    }

    public void WriteAllLines(Fil fil, IEnumerable<string> content)
    {
        WriteAllText(fil, string.Join('\n', content));
    }

    public Task WriteAllLinesAsync(Fil fil, IEnumerable<string> contents)
    {
        throw new NotImplementedException();
    }

    public bool DirectoryExists(Dir dir)
    {
        var dirs = Split(dir);
        var di = GetDir(dirs);
        return di != null;
    }

    public void CreateDirectory(Dir dir)
    {
        var entries = Split(dir);
        var r = root;
        foreach (var entry in entries)
        {
            if (r.Dirs.TryGetValue(entry, out var next))
            {
                r = next;
            }
            else
            {
                next = new Entry();
                r.Dirs.Add(entry, next);
                r = next;
            }
        }
    }

    public IEnumerable<Fil> EnumerateFiles(Dir dir)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<Dir> EnumerateDirs(Dir dir)
    {
        throw new NotImplementedException();
    }

    public bool FileExists(Fil fil)
    {
        var (dirs, name) = Split(fil);
        var di = GetDir(dirs);
        if (di == null) return false;

        return di.Files.ContainsKey(name);
    }

    public DateTime LastWriteTimeUtc(Fil fil)
    {
        throw new NotImplementedException();
    }

    public UnixFileMode UnixFileMode(Fil fil)
    {
        throw new NotImplementedException();
    }

    public void AddContent(Fil fil, string content)
    {
        var d = fil.Directory;
        if (d == null) throw new Exception("Expected a dir");
        if (DirectoryExists(d) == false)
        {
            CreateDirectory(d);
        }
        WriteAllText(fil, content);
    }

    public object GetContent(Fil fil) => ReadAllText(fil);
}

internal class LoggableTest : Log
{
    public List<string> Errors { get; } = new();

    public bool HasError()
    {
        return Errors.Count > 0;
    }

    public void Error(FileLine? file, string message)
    {
        throw new NotImplementedException();
    }

    public void Error(string message)
    {
        Errors.Add(message);
    }

    public void Warning(string message)
    {
        throw new NotImplementedException();
    }

    public void Print(MessageType message_type, FileLine? file, string message, string code)
    {
        throw new NotImplementedException();
    }

    public void PrintError(FileLine? file, string message, string? code)
    {
        throw new NotImplementedException();
    }

    public void WriteInformation(FileLine? file, string message)
    {
        throw new NotImplementedException();
    }

    internal string GetOutput()
    {
        return string.Join("\n", Errors);
    }
}

public class TestBase
{
    internal LoggableTest log = new();
    internal VfsTest vfs = new();
}
