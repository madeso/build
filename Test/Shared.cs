﻿using System.IO;
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

    public string ReadAllText(Fil fil)
    {
        return files[fil.Path];
    }

    public IEnumerable<string> ReadAllLines(Fil fil)
    {
        return files[fil.Path].Split('\n');
    }

    public Task<string[]> ReadAllLinesAsync(Fil fil)
    {
        return Task<string[]>.Factory.StartNew(() => files[fil.Path].Split('\n'));
    }

    private readonly Dictionary<string, string> files_to_write = new();

    public Task WriteAllTextAsync(Fil path, string contents)
    {
        return Task.Factory.StartNew(() => { files_to_write.Add(path.Path, contents); });
    }

    public string GetContent(Fil file)
    {
        if (files_to_write.Remove(file.Path, out var content))
        {
            return content;
        }
        else
        {
            throw new FileNotFoundException($"{file.Path} was not added to container.\n{GetRemainingFilesAsText()}");
        }
    }

    public IEnumerable<string> GetLines(Fil file) => GetContent(file).Split('\n', StringSplitOptions.TrimEntries).Where(s => string.IsNullOrWhiteSpace(s) == false);

    public IEnumerable<string> RemainingFiles => files_to_write.Keys;

    internal bool IsEmpty()
    {
        return files_to_write.Count == 0;
    }

    internal string GetRemainingFilesAsText()
    {
        var fs = string.Join(" ", files_to_write.Keys);
        return $"files: [{fs}]";
    }

    public void WriteAllText(Fil fil, string content)
    {
        files_to_write.Add(fil.Path, content);
    }

    public void WriteAllLines(Fil fil, IEnumerable<string> content)
    {
        files_to_write.Add(fil.Path, string.Join("\n", content));
    }

    public Task WriteAllLinesAsync(Fil fil, IEnumerable<string> contents)
    {
        return Task.Factory.StartNew(() => files_to_write.Add(fil.Path, string.Join("\n", contents)));
    }
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
