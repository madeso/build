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
        Path = SysPath.GetFullPath(new DirectoryInfo(path).FullName);
    }

    public bool Exists => Directory.Exists(Path);
    public string Name => new DirectoryInfo(Path).Name;

    public static Dir? ToExistingDirOrNull(string arg)
    {
        var d = new Dir(arg);
        return d.Exists == false ? null : d;
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

    public void CreateDir()
    {
        if (Exists)
        {
            return;
        }

        Directory.CreateDirectory(Path);
    }

    public IEnumerable<Fil> EnumerateFiles()
        => Exists
            ? new DirectoryInfo(Path).EnumerateFiles()
            .Select(f => new Fil(f.FullName))
            : Array.Empty<Fil>()
            ;

    public IEnumerable<Dir> EnumerateDirectories()
        => new DirectoryInfo(Path).EnumerateDirectories()
            .Select(d => new Dir(d.FullName));

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

    public string GetDisplay()
        => Dir.CurrentDirectory.RelativeFromTo(this);
}

public class Fil : IComparable<Fil>
{
    public string Path { get; init; }

    public Fil(string path)
    {
        Debug.Assert(SysPath.IsPathFullyQualified(path), "file path must be rooted");
        Path = SysPath.GetFullPath(new FileInfo(path).FullName);
    }

    public string GetRelativeOrFullPath(Dir? a_root_relative = null)
    {
        var root_relative = a_root_relative ?? Dir.CurrentDirectory;
        var suggested = root_relative.RelativeFromTo(this);

        // if returned path includes back references, just use full path
        if (suggested.StartsWith(".")) return Path;

        return suggested;
    }

    public bool Exists => File.Exists(Path);

    public Dir? Directory => Dir.Create(new FileInfo(Path).Directory?.FullName);
    public string Name => new FileInfo(Path).Name;
    public string NameWithoutExtension => SysPath.GetFileNameWithoutExtension(Path);
    public string Extension => SysPath.GetExtension(Path);
    public DateTime LastWriteTimeUtc => new FileInfo(Path).LastWriteTimeUtc;
    public UnixFileMode UnixFileMode => new FileInfo(Path).UnixFileMode;

    public string GetDisplay() => Dir.CurrentDirectory.GetRelativeTo(this);
    public string GetRelative(Dir root) => root.GetRelativeTo(this);

    public bool IsInFolder(Dir folder) => FileUtil.FileIsInFolder(this, folder);

    public string ReadAllText() => File.ReadAllText(Path, System.Text.Encoding.UTF8);
    public IEnumerable<string> ReadAllLines() => File.ReadLines(Path, System.Text.Encoding.UTF8);

    public void WriteAllText(string content) => File.WriteAllText(Path, content, System.Text.Encoding.UTF8);
    public void WriteAllLines(IEnumerable<string> content) => File.WriteAllLines(Path, content, System.Text.Encoding.UTF8);

    public static Fil? ToExistingDirOrNull(string arg)
    {
        var d = new Fil(arg);
        return d.Exists == false ? null : d;
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

    public Task WriteAllLinesAsync(IEnumerable<string> contents)
        => File.WriteAllLinesAsync(Path, contents, System.Text.Encoding.UTF8);

    public Task<string[]> ReadAllLinesAsync()
        => File.ReadAllLinesAsync(Path, System.Text.Encoding.UTF8);
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
