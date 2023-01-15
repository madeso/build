using System.Collections.Immutable;

namespace Workbench.Hero.Data;

///////////////////////////////////////////////////////////////////////////////////////////////////
// Project

// use root to create pretty display names
public record OutputFolders(string InputRoot, string OutputDirectory);

public class UserInput
{
    public List<string> ProjectDirectories {get;}= new();
    public List<string> IncludeDirectories {get;}= new();
    public List<string> PrecompiledHeaders { get; } = new();

    public static UserInput? LoadFromFile(Printer print, string file)
    {
        var content = File.ReadAllText(file);
        var data = JsonUtil.Parse<UserInput>(print, file, content);
        return data;
    }

    public void Decorate(Printer printer, string root)
    {
        DecorateThis(printer, root, ProjectDirectories, "Project directories", f => Directory.Exists(f) || File.Exists(f));
        DecorateThis(printer, root, IncludeDirectories, "Include directories", Directory.Exists);

        static void DecorateThis(Printer printer, string root, List<string> d, string name, Func<string, bool> exists)
        {
            var missing = d.Where(d => Path.IsPathFullyQualified(d) == false || exists(d) == false).ToImmutableHashSet();
            var changes = missing
                .Select(d => new { Src = d, Dst = Path.Join(root, d) })
                .Select(d => new { Src = d.Src, Dst = d.Dst, Exist = exists(d.Dst) })
                .ToImmutableArray()
                ;

            foreach (var x in changes.Where(x => x.Exist == true))
            {
                printer.Info($"{x.Src} does not exist in {name}, but was replaced with {x.Dst}");
            }

            foreach (var x in changes.Where(x => x.Exist == false))
            {
                printer.Info($"{x.Src} was removed from {name} since it doesn't exist and the replacement {x.Dst} was found");
            }

            d.RemoveAll(missing.Contains);
            foreach (var f in d)
            {
                printer.Info($"{f} was kept in {name}");
            }
            d.AddRange(changes.Where(x => x.Exist).Select(x => x.Dst));
        }
    }
}

public class Project
{
    public ImmutableArray<string> ScanDirectories {get;}
    public ImmutableArray<string> IncludeDirectories {get;}
    public ImmutableArray<string> PrecompiledHeaders { get; }
    public Dictionary<string, SourceFile> ScannedFiles { get; } = new();
    // public DateTime LastScan,

    public Project(UserInput input)
    {
        ScanDirectories = input.ProjectDirectories.ToImmutableArray();
        IncludeDirectories = input.IncludeDirectories.ToImmutableArray();
        PrecompiledHeaders = input.PrecompiledHeaders.ToImmutableArray();
        // LastScan = DateTime.Now
    }

    // public void Clean()
    // {
    //     this.scanned_files.clear();
    // }
}

///////////////////////////////////////////////////////////////////////////////////////////////////
// SourceFile

public class SourceFile
{
    public List<string> LocalIncludes { get; } = new();
    public List<string> SystemIncludes { get; } = new();
    public List<string> AbsoluteIncludes { get; set; } = new(); // change to a hash set?
    public int NumberOfLines { get; set; } = 0;
    public bool IsTouched { get; set; } = false;
    public bool IsPrecompiled { get; set; } = false;

    private SourceFile()
    {
    }

    public SourceFile(int numberOfLines, List<string> localIncludes, List<string> systemIncludes, bool isPrecompiled)
    {
        LocalIncludes = localIncludes;
        SystemIncludes = systemIncludes;
        NumberOfLines = numberOfLines;
        IsPrecompiled = isPrecompiled;
    }
}
