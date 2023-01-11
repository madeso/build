using System.Collections.Immutable;
using System.Xml.Serialization;

namespace Workbench.Hero.Data;

///////////////////////////////////////////////////////////////////////////////////////////////////
// Project

// use root to create pretty display names
public record OutputFolders (string InputRoot, string OutputDirectory);

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
            var missing = d.Where(d => Path.IsPathFullyQualified(d)== false || exists(d) == false).ToImmutableHashSet();
            var changes = missing
                .Select(d => new { Src = d, Dst = Path.Join(root, d) })
                .Select(d => new { Src = d.Src, Dst = d.Dst, Exist = exists(d.Dst) })
                .ToImmutableArray()
                ;

            foreach (var x in changes.Where(x => x.Exist == true))
            {
                printer.info($"{x.Src} does not exist in {name}, but was replaced with {x.Dst}");
            }

            foreach (var x in changes.Where(x => x.Exist == false))
            {
                printer.info($"{x.Src} was removed from {name} since it doesn't exist and the replacement {x.Dst} was found");
            }

            d.RemoveAll(missing.Contains);
            foreach(var f in d)
            {
                printer.info($"{f} was kept in {name}");
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
    public readonly List<string> local_includes = new();
    public readonly List<string> system_includes = new();
    public List<string> absolute_includes = new(); // change to a hash set?
    public int number_of_lines = 0;
    public bool is_touched = false;
    public bool is_precompiled = false;

    private SourceFile()
    {
    }

    public SourceFile (int number_of_lines, List<string> local_includes, List<string> system_includes, bool is_precompiled)
    {
        this.local_includes = local_includes;
        this.system_includes = system_includes;
        this.number_of_lines = number_of_lines;
        this.is_precompiled = is_precompiled;
    }
}

public static class Utils
{

    public static bool is_translation_unit_extension(string ext)
    {
        return ext switch
        {
            ".cpp" or ".c" or ".cc" or ".cxx" or ".mm" or ".m" => true,
            _ => false,
        };
    }

    public static bool is_translation_unit(string path)
    {
        return is_translation_unit_extension(Path.GetExtension(path));
    }
}
