using System.Collections.Immutable;
using Spectre.Console;
using Workbench.Shared;

namespace Workbench.Commands.Hero;

///////////////////////////////////////////////////////////////////////////////////////////////////
// Project

// use root to create pretty display names
public record OutputFolders(string InputRoot, string OutputDirectory);

public class UserInput
{
    public List<string> ProjectDirectories { get; set; } = new();
    public List<string> IncludeDirectories { get; set; } = new();
    public List<string> PrecompiledHeaders { get; set; } = new();

    public bool Validate(Log print)
    {
        var status = true;

        if (ProjectDirectories.Count == 0)
        {
            status = false;
            print.Error("Project directories are empty");
        }

        if (IncludeDirectories.Count == 0)
        {
            status = false;
            print.Error("Include directories are empty");
        }

        return status;
    }

    public static UserInput? LoadFromFile(Log print, string file)
    {
        var content = File.ReadAllText(file);
        var data = JsonUtil.Parse<UserInput>(print, file, content);
        return data;
    }

    public void Decorate(Log log, string root)
    {
        decorate_this(root, ProjectDirectories, "Project directories", f => Directory.Exists(f) || File.Exists(f));
        decorate_this(root, IncludeDirectories, "Include directories", Directory.Exists);
        return;

        static void decorate_this(string root, List<string> d, string name, Func<string, bool> exists)
        {
            var missing = d.Where(d => Path.IsPathFullyQualified(d) == false || exists(d) == false).ToImmutableHashSet();
            var changes = missing
                .Select(d => new { Src = d, Dst = Path.Join(root, d) })
                .Select(d => new { d.Src, d.Dst, Exist = exists(d.Dst) })
                .ToImmutableArray()
                ;

            foreach (var x in changes.Where(x => x.Exist == true))
            {
                AnsiConsole.WriteLine($"{x.Src} does not exist in {name}, but was replaced with {x.Dst}");
            }

            foreach (var x in changes.Where(x => x.Exist == false))
            {
                AnsiConsole.WriteLine($"{x.Src} was removed from {name} since it doesn't exist and the replacement {x.Dst} was found");
            }

            d.RemoveAll(missing.Contains);
            foreach (var f in d)
            {
                AnsiConsole.WriteLine($"{f} was kept in {name}");
            }
            d.AddRange(changes.Where(x => x.Exist).Select(x => x.Dst));
        }
    }
}

public class Project
{
    public ImmutableArray<string> ScanDirectories { get; }
    public ImmutableArray<string> IncludeDirectories { get; }
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
    public List<string> LocalIncludes { get; }
    public List<string> SystemIncludes { get; }
    public int NumberOfLines { get; set; }
    public bool IsPrecompiled { get; set; }

    public List<string> AbsoluteIncludes { get; set; } // change to a hash set?
    public bool IsTouched { get; set; }

    public SourceFile(int number_of_lines, List<string> local_includes, List<string> system_includes, bool is_precompiled)
    {
        LocalIncludes = local_includes;
        SystemIncludes = system_includes;
        NumberOfLines = number_of_lines;
        IsPrecompiled = is_precompiled;

        AbsoluteIncludes = new List<string>();
        IsTouched = false;
    }
}
