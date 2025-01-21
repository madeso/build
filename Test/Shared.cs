using System.Collections.Immutable;
using System.IO;
using Microsoft.CSharp.RuntimeBinder;
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
    class FileContent(string content)
    {
        public string Content { get; } = content;
        public DateTime Time { get; } = DateTime.Now;
    }
    class Entry
    {
        public Dictionary<string, FileContent> Files { get; } = new();
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
        if (di.Files.TryGetValue(file, out var value)) return value.Content;
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
        di.Files[file] = new FileContent(content);
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
        var d = RequireDir(Split(dir));
        return d.Files.Keys.Select(dir.GetFile);
    }

    public IEnumerable<Dir> EnumerateDirs(Dir dir)
    {
        var d = RequireDir(Split(dir));
        return d.Dirs.Keys.Select(f => dir.GetSubDirs(f));
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
        var (dirs, name) = Split(fil);
        var di = GetDir(dirs);
        if (di == null) return DateTime.Now;

        if (di.Files.TryGetValue(name, out var entry)) return entry.Time;

        return DateTime.Now;
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
    public record Entry(MessageType Type, string Message);
    public List<Entry> AllMessages { get; } = new();

    public void Error(FileLine? file, string message, string? code = null)
    {
        AllMessages.Add(new(MessageType.Error, $"{Log.ToFileString(file)}: {Log.WithCode(MessageType.Error, code)}: {message}"));
    }

    public void Error(string message)
    {
        AllMessages.Add(new(MessageType.Error, $"ERROR: {message}"));
    }

    public void Warning(FileLine? file, string message, string? code = null)
    {
        AllMessages.Add(new(MessageType.Warning, $"{Log.ToFileString(file)}: {Log.WithCode(MessageType.Warning, code)}: {message}"));
    }

    public void Warning(string message)
    {
        AllMessages.Add(new(MessageType.Warning, $"WARNING: {message}"));
    }

    public void Info(FileLine? file, string message, string? code = null)
    {
        AllMessages.Add(new(MessageType.Info, $"{Log.ToFileString(file)}: {Log.WithCode(MessageType.Info, code)}: {message}"));
    }

    public void Info(string message)
    {
        AllMessages.Add(new(MessageType.Info, message));
    }

    public string Print()
    {
        return string.Join("\n", AllMessages.Select(m => m.Message));
    }

    public IEnumerable<string> ErrorsAndWarnings
        => AllMessages
            .Where(m => m.Type is MessageType.Error or MessageType.Warning)
            .Select(m => m.Message);
}

public class NoRunExecutor : Executor
{
    public Task<ProcessExit> RunWithCallbackAsync(ImmutableArray<string> arguments, Fil exe, Dir cwd, IEnumerable<string>? input, Action<string> on_stdout, Action<string> on_stderr, Action<string, Exception> on_fail)
    {
        throw new NotImplementedException();
    }
}

public class DefinedExecutor : Executor
{
    internal record Entry(Fil Exe, Dir cwd, int Arg, string What, ProcessExit Run);
    private List<Entry> entries = new();

    internal void Add(Entry e)
    {
        entries.Add(e);
    }

    public Task<ProcessExit> RunWithCallbackAsync(ImmutableArray<string> arguments, Fil exe, Dir cwd, IEnumerable<string>? input, Action<string> on_stdout, Action<string> on_stderr, Action<string, Exception> on_fail)
    {
        return Task.Run(() =>
        {
            foreach (var e in entries)
            {
                if (!Equals(e.cwd, cwd)) continue;
                if(!Equals(e.Exe, exe)) continue;
                if (e.Arg >= arguments.Length) throw new Exception($"Invalid arg: {arguments.Length} but requested {e.Arg}: {ExecHelper.CollapseArgStringToSingle(arguments, PlatformID.Win32Windows)}");
                if(arguments[e.Arg] != e.What) continue;
                return e.Run;
            }

            throw new Exception($"no registered output: {exe}: {ExecHelper.CollapseArgStringToSingle(arguments, PlatformID.Win32Windows)} in {cwd}");
        });
    }
}

public class TestBase
{
    internal LoggableTest log = new();
    internal VfsTest vfs = new();

    internal NoRunExecutor no_run_executor = new();
    internal DefinedExecutor exec = new();
}
