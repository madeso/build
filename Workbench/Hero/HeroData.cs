using System.Collections.Immutable;

namespace Workbench.Hero.Data;

///////////////////////////////////////////////////////////////////////////////////////////////////
// Project

// use root to create pretty display names
public record OutputFolders(string InputRoot, string OutputDirectory);

public class UserInput
{
    public List<string> project_directories = new();
    public List<string> include_directories = new();
    public List<string> precompiled_headers = new();

    public static UserInput? load_from_file(Printer print, string file)
    {
        var content = File.ReadAllText(file);
        var data = JsonUtil.Parse<UserInput>(print, file, content);
        return data;
    }

    public void decorate(Printer printer, string root)
    {
        decorate_this(printer, root, this.project_directories, "Project directories", f => Directory.Exists(f) || File.Exists(f));
        decorate_this(printer, root, this.include_directories, "Include directories", Directory.Exists);

        static void decorate_this(Printer printer, string root, List<string> d, string name, Func<string, bool> exists)
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
    public List<string> scan_directories;
    public List<string> include_directories;
    public List<string> precompiled_headers;
    public Dictionary<string, SourceFile> scanned_files = new();
    // public DateTime LastScan,

    public Project(UserInput input)
    {
        scan_directories = input.project_directories;
        include_directories = input.include_directories;
        precompiled_headers = input.precompiled_headers;
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
        this.LocalIncludes = localIncludes;
        this.SystemIncludes = systemIncludes;
        this.NumberOfLines = numberOfLines;
        this.IsPrecompiled = isPrecompiled;
    }
}

public static class Utils
{
    public static bool IsTranslationUnitExtension(string ext)
    {
        return ext switch
        {
            ".cpp" or ".c" or ".cc" or ".cxx" or ".mm" or ".m" => true,
            _ => false,
        };
    }

    public static bool IsTranslationUnit(string path)
    {
        return IsTranslationUnitExtension(Path.GetExtension(path));
    }
}
