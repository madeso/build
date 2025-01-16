using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Spectre.Console;
using Workbench.Commands.Status;
using Workbench.Shared;
using Workbench.Shared.Extensions;

namespace Workbench.Commands.Hero;

///////////////////////////////////////////////////////////////////////////////////////////////////
// Project

// use root to create pretty display names
public record OutputFolders(Dir InputRoot, Dir OutputDirectory);

public class UserInput
{
    [JsonPropertyName("project_directories")]
    public List<string> ProjectDirectories { get; set; } = new();

    [JsonPropertyName("include_directories")]
    public List<string> IncludeDirectories { get; set; } = new();

    [JsonPropertyName("pre_compiled_headers")]
    public List<string> PrecompiledHeaders { get; set; } = new();

    public static UserInput? LoadFromFile(VfsRead vread, Log print, Fil file)
    {
        var content = file.ReadAllText(vread);
        var data = JsonUtil.Parse<UserInput>(print, file, content);
        return data;
    }

    public Project? ToProject(Log log, Dir root, Project? previous_project)
    {
        var project_dirs = decorate_this(log, root, ProjectDirectories, "Project directories", f => Directory.Exists(f) || File.Exists(f))
            .Select(FileOrDir.FromExistingOrNull)
            .IgnoreNull()
            .ToImmutableArray()
            ;
        var include_dirs = decorate_this(log, root, IncludeDirectories, "Include directories", Directory.Exists)
            .Select(p => new Dir(p))
            .ToImmutableArray()
            ;
        var pch_files = decorate_this(log, root, PrecompiledHeaders, "Precompiled headers", File.Exists)
            .Select(p => new Fil(p))
            .ToImmutableArray()
            ;

        bool status = true;

        if (project_dirs.Length == 0)
        {
            log.Error("Project directories are empty");
            status = false;
        }

        if (include_dirs.Length == 0)
        {
            status = false;
            log.Error("Include directories are empty");
        }

        
        return status ? new Project(project_dirs, include_dirs, pch_files, previous_project) : null;

        static IEnumerable<string> decorate_this(Log log, Dir root, IEnumerable<string> src, string name, Func<string, bool> exists)
        {
            return src
                .Select(d => new
                {
                    Src = d,
                    IsRelative = !Path.IsPathFullyQualified(d),
                    Dst = FileUtil.RootPath(root, d)
                })
                .Where(x => exists(x.Dst), x =>
                {
                    log.Warning($"{x.Src} was removed from {name} since it doesn't exist and the replacement {x.Dst} wasn't found");
                })
                .Select(x => x.Dst);
        }
    }
}

public class Project
{
    public ImmutableArray<FileOrDir> ScanDirectories { get; }
    public ImmutableArray<Dir> IncludeDirectories { get; }
    public ImmutableArray<Fil> PrecompiledHeaders { get; }
    public Dictionary<Fil, SourceFile> ScannedFiles { get; }
    // public DateTime LastScan,

    public Project(ImmutableArray<FileOrDir> project_directories,
        ImmutableArray<Dir> include_directories, ImmutableArray<Fil> precompiled_headers, Project? project)
    {
        ScanDirectories = project_directories;
        IncludeDirectories = include_directories;
        PrecompiledHeaders = precompiled_headers;
        ScannedFiles = project != null ? project.ScannedFiles : new();
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

    public List<Fil> AbsoluteIncludes { get; set; } // change to a hash set?
    public bool IsTouched { get; set; }

    public SourceFile(int number_of_lines, List<string> local_includes,
        List<string> system_includes, bool is_precompiled)
    {
        LocalIncludes = local_includes;
        SystemIncludes = system_includes;
        NumberOfLines = number_of_lines;
        IsPrecompiled = is_precompiled;

        AbsoluteIncludes = new();
        IsTouched = false;
    }
}
