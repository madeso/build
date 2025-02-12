using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Workbench.Shared;

using SysPath = System.IO.Path;

public class FileOrDir
{
    public string Path { get; init; }
    public bool IsDirectory { get; init; }
    
    public FileOrDir(Fil fil)
    {
        Path = fil.Path;
        IsDirectory = false;
    }

    public FileOrDir(Dir dir)
    {
        Path = dir.Path;
        IsDirectory = true;
    }

    public bool Exists => IsDirectory ? Directory.Exists(Path) : File.Exists(Path);
    public bool IsFile => IsDirectory == false;

    public Fil? AsFile => IsDirectory == false ? new Fil(Path) : null;
    public Dir? AsDir => IsDirectory == true ? new Dir(Path) : null;

    public override string ToString() => Path;
    public override int GetHashCode() => Path.GetHashCode();
    public override bool Equals(object? obj)
    {
        if(obj is not FileOrDir rhs) return false;
        return Path == rhs.Path && IsDirectory == rhs.IsDirectory;
    }

    public static FileOrDir? FromExistingOrNull(string path)
    {
        if (File.Exists(path))
        {
            return new FileOrDir(new Fil(path));
        } else if (Directory.Exists(path))
        {
            return new FileOrDir(new Dir(path));
        }
        else
        {
            return null;
        }
    }
}

public class Dir
{
    public string Path { get; init; }

    public Dir(string path)
    {
        Debug.Assert(SysPath.IsPathFullyQualified(path), "dir path must be rooted");
        var p = SysPath.GetFullPath(new DirectoryInfo(path).FullName);
        // Debug.Assert(p == path, "complex code actually does something that might be needed yo move to the vfs", "{0} != {1}", p, path);
        Path = path;
    }

    public bool Exists(Vfs v) => v.DirectoryExists(this);

    public string Name => new DirectoryInfo(Path).Name;

    public static Dir? ToExistingDirOrNull(Vfs v, string arg)
    {
        var d = new Dir(arg);
        return d.Exists(v) == false ? null : d;
    }

    public static Dir CurrentDirectory => new(Environment.CurrentDirectory);

    public Dir? Parent
    {
        get
        {
            var p = new DirectoryInfo(Path).Parent;
            return p == null ? null : new Dir(p.FullName);
        }
    }

    public static Dir? Create(string? path) => path == null ? null : new Dir(path);

    public bool HasFile(Fil f) => GetRelativeTo(f).StartsWith("..") == false;


    public Fil GetFile(string file) => new(SysPath.Join(Path, file));

    public Dir GetDir(string sub) => new(SysPath.Join(Path, sub));

    public Dir GetSubDirs(IEnumerable<string> sub)
        => sub.Aggregate(this, (current, name) => current.GetDir(name));

    public Dir GetSubDirs(params string[] sub)
        => sub.Aggregate(this, (current, name) => current.GetDir(name));

    public void CreateDir(Vfs v)
    {
        if (Exists(v))
        {
            return;
        }

        v.CreateDirectory(this);
    }

    public IEnumerable<Fil> EnumerateFiles(Vfs v)
        => v.EnumerateFiles(this);

    public IEnumerable<Dir> EnumerateDirectories(Vfs v)
        => v.EnumerateDirs(this);

    public override string ToString() => Path;
    public override int GetHashCode() => Path.GetHashCode();
    public override bool Equals(object? obj)
    {
        var rhs = obj as Dir;
        if(rhs == null) return false;
        return Path == rhs.Path;
    }

    // todo(Gustav): Rename all relative to RelativeFromTo
    public string RelativeFromTo(Dir to) => SysPath.GetRelativePath(Path, to.Path);
    public string GetRelativeTo(Fil f) => SysPath.GetRelativePath(Path, f.Path);
    public string RelativeFromTo(Fil to) => SysPath.GetRelativePath(Path, to.Path);

    public string GetDisplay(Dir cwd)
        => cwd.RelativeFromTo(this);
}

public class Fil : IComparable<Fil>
{
    public string Path { get; init; }

    public Fil(string path)
    {
        Debug.Assert(SysPath.IsPathFullyQualified(path), $"file path must be rooted, {path} wasn't");
        var p = SysPath.GetFullPath(new FileInfo(path).FullName);
        Debug.Assert(p == path, $"complex code actually does something: {p} != {path}");
        Path = path;
    }

    public string GetRelativeOrFullPath(Dir cwd, Dir? a_root_relative = null)
    {
        // merge and rename to GetDisplay
        var root_relative = a_root_relative ?? cwd;
        var suggested = root_relative.RelativeFromTo(this);

        // if returned path includes back references, just use full path
        if (suggested.StartsWith(".")) return Path;

        return suggested;
    }

    public bool Exists(Vfs v) => v.FileExists(this);

    public Dir? Directory => Dir.Create(new FileInfo(Path).Directory?.FullName);
    public string Name => new FileInfo(Path).Name;
    public string NameWithoutExtension => SysPath.GetFileNameWithoutExtension(Path);
    public string Extension => SysPath.GetExtension(Path);

    public DateTime LastWriteTimeUtc(Vfs v) => v.LastWriteTimeUtc(this);
    public UnixFileMode UnixFileMode(Vfs v) => v.UnixFileMode(this);

    public string GetDisplay(Dir cwd) => cwd.GetRelativeTo(this);
    public string GetRelative(Dir root) => root.GetRelativeTo(this);

    public bool IsInFolder(Dir folder) => FileUtil.FileIsInFolder(this, folder);

    public string ReadAllText(Vfs vfs) => vfs.ReadAllText(this);
    public IEnumerable<string> ReadAllLines(Vfs vfs) => vfs.ReadAllLines(this);

    public void WriteAllText(Vfs vfs, string content) => vfs.WriteAllText(this, content);
    public void WriteAllLines(Vfs vfs, IEnumerable<string> content) => vfs.WriteAllLines(this, content);

    public static Fil? ToExistingDirOrNull(Vfs vfs, string arg)
    {
        var d = new Fil(arg);
        return d.Exists(vfs) == false ? null : d;
    }

    public override string ToString() => Path;
    public override int GetHashCode() => Path.GetHashCode();
    
    public int CompareTo(Fil? other)
    {
        return string.Compare(Path, other?.Path, StringComparison.InvariantCulture);
    }

    public override bool Equals(object? obj)
        => obj is Fil rhs && EqualTo(rhs);
    public bool EqualTo(Fil rhs)
        => Path == rhs.Path;

    public Fil ChangeExtension(string new_extension)
    {
        var dir = Directory!;
        var name = NameWithoutExtension;
        return new Fil(SysPath.Join(dir.Path, $"{name}{new_extension}"));
    }

    public Task WriteAllLinesAsync(Vfs vfs, IEnumerable<string> contents)
        => vfs.WriteAllLinesAsync(this, contents);

    public Task<string[]> ReadAllLinesAsync(Vfs vfs)
        => vfs.ReadAllLinesAsync(this);
}



public interface Vfs
{
    string ReadAllText(Fil fil);
    IEnumerable<string> ReadAllLines(Fil fil);
    Task<string[]> ReadAllLinesAsync(Fil fil);

    void WriteAllText(Fil fil, string content);
    void WriteAllLines(Fil fil, IEnumerable<string> content);
    Task WriteAllLinesAsync(Fil fil, IEnumerable<string> contents);
    bool DirectoryExists(Dir dir);
    void CreateDirectory(Dir dir);
    IEnumerable<Fil> EnumerateFiles(Dir dir);
    IEnumerable<Dir> EnumerateDirs(Dir dir);
    bool FileExists(Fil fil);
    DateTime LastWriteTimeUtc(Fil fil);
    UnixFileMode UnixFileMode(Fil fil);
}

public class VfsDisk : Vfs
{
    public string ReadAllText(Fil fil)
        => File.ReadAllText(fil.Path, System.Text.Encoding.UTF8);

    public IEnumerable<string> ReadAllLines(Fil fil)
        => File.ReadLines(fil.Path, System.Text.Encoding.UTF8);

    public Task<string[]> ReadAllLinesAsync(Fil fil)
        => File.ReadAllLinesAsync(fil.Path, System.Text.Encoding.UTF8);

    public void WriteAllText(Fil fil, string content)
        => File.WriteAllText(fil.Path, content, System.Text.Encoding.UTF8);

    public void WriteAllLines(Fil fil, IEnumerable<string> content)
        => File.WriteAllLines(fil.Path, content, System.Text.Encoding.UTF8);

    public Task WriteAllLinesAsync(Fil fil, IEnumerable<string> contents)
        => File.WriteAllLinesAsync(fil.Path, contents, System.Text.Encoding.UTF8);

    public bool DirectoryExists(Dir dir)
        => Directory.Exists(dir.Path);

    public void CreateDirectory(Dir dir)
    {
        Directory.CreateDirectory(dir.Path);
    }

    public IEnumerable<Fil> EnumerateFiles(Dir dir)
        => DirectoryExists(dir)
            ? new DirectoryInfo(dir.Path).EnumerateFiles()
                .Select(f => new Fil(f.FullName))
            : Array.Empty<Fil>()
    ;

    public IEnumerable<Dir> EnumerateDirs(Dir dir)
        => new DirectoryInfo(dir.Path).EnumerateDirectories()
         .Select(d => new Dir(d.FullName));

    public bool FileExists(Fil fil)
        => File.Exists(fil.Path);

    public DateTime LastWriteTimeUtc(Fil fil) => new FileInfo(fil.Path).LastWriteTimeUtc;
    public UnixFileMode UnixFileMode(Fil fil) => new FileInfo(fil.Path).UnixFileMode;
}


// json serialization
public class FilJsonConverter : JsonConverter<Fil>
{
    public override Fil Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
        => new(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, Fil fil, JsonSerializerOptions o)
        => writer.WriteStringValue(fil.Path);
}

public class DirJsonConverter : JsonConverter<Dir>
{
    public override Dir Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
        => new(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, Dir fil, JsonSerializerOptions o)
        => writer.WriteStringValue(fil.Path);
}
