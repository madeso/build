using System.Diagnostics;

namespace Workbench.Shared;

public enum Language
{
    CSharp,
    ReactJavascript,
    ReactTypescript,
    Typescript,
    Javascript,
    CppSource,
    CppHeader,
    Unknown,
    ObjectiveCpp,
    JsonConfig,
    Swift,
    CMake,
    Razor,
    Css
}

internal static class FileUtil
{
    internal static bool IsHeaderOrSource(Fil path)
        => IsHeader(path) || IsSource(path);

    internal static bool IsSource(Fil path)
        => ClassifySource(path) == Language.CppSource;

    internal static bool IsHeader(Fil path)
        => ClassifySource(path) == Language.CppHeader;

    public static bool IsTranslationUnit(Fil path)
        => ClassifySource(path) is Language.CppSource or Language.ObjectiveCpp;

    public static Language ClassifySource(Fil f)
        => f.Name == "CMakeLists.txt" ? Language.CMake : f.Extension switch
        {
            ".cs" => Language.CSharp,
            ".tsx" => Language.ReactTypescript,
            ".jsx" => Language.ReactJavascript,
            ".js" => Language.Javascript,
            ".ts" => Language.Typescript,
            ".swift" => Language.Swift,
            
            ".razor" => Language.Razor,
            ".css" => Language.Css,

            ".jsonc" => Language.JsonConfig,

            ".cmake" => Language.CMake,

            ".cc" or ".cpp" or ".c" or ".cxx" => Language.CppSource,
            ".hh" or ".hpp" or ".h" or ".hxx" or ".inl" or "" => Language.CppHeader,

            ".mm" or ".m" => Language.ObjectiveCpp,
            _ => Language.Unknown,
        };

    public static string? ClassifySourceOrNull(Fil f)
        => ClassifySource(f) switch
        {
            Language.CSharp => "C#",
            Language.ReactJavascript => "React (js)",
            Language.ReactTypescript => "React (ts)",
            Language.Typescript => "TypeScript",
            Language.Javascript => "JavaScript",
            Language.JsonConfig => "JSON config",
            Language.Swift => "Swift",
            Language.Razor => "Razor",
            Language.Css => "CSS",
            Language.CppSource or Language.CppHeader => "C/C++",
            Language.ObjectiveCpp => "Objective-C/C++",
            Language.CMake => "CMake",
            Language.Unknown => null,
            // why is a invalid enum value a valid value???
            _ => throw new ArgumentOutOfRangeException()
        };

    public static string ToString(Language lang)
        => lang switch
        {
            Language.CSharp => "C#",
            Language.ReactJavascript => "React (js)",
            Language.ReactTypescript => "React (ts)",
            Language.Typescript => "TypeScript",
            Language.Javascript => "JavaScript",
            Language.JsonConfig => "JSON config",
            Language.Swift => "Swift",
            Language.Razor => "Razor",
            Language.Css => "CSS",
            Language.CppSource => "C/C++ Source",
            Language.CppHeader => "C/C++ Header",
            Language.ObjectiveCpp => "Objective-C/C++",
            Language.CMake => "CMake",
            Language.Unknown => "Unknown",
            // why is a invalid enum value a valid value???
            _ => throw new ArgumentOutOfRangeException()
        };

    public static readonly string[] PitchforkFolders = { "apps", "libs", "src", "include" };

    public static IEnumerable<Dir> PitchforkBuildFolders(Vfs vfs, Dir root)
    {
        var build = root.GetDir("build");
        if (!build.Exists(vfs))
        {
            yield break;
        }

        yield return build;

        foreach (var d in build.EnumerateDirectories(vfs))
        {
            yield return d;
        }
    }

    public static IEnumerable<Fil> SourcesFromArgs(Vfs vfs, Dir cwd, IEnumerable<string> args, Func<Fil, bool> these_files)
        => ListFilesFromArgs(vfs, cwd, args)
            .Where(these_files);

    public static IEnumerable<Fil> ListFilesFromArgs(Vfs vfs, Dir cwd, IEnumerable<string> args)
    {
        foreach (var unrooted_file_or_dir in args)
        {
            var file_or_dir = RootPath(cwd, unrooted_file_or_dir);
            if (File.Exists(file_or_dir)) yield return new Fil(file_or_dir);
            else // assume directory
            {
                foreach (var f in IterateFiles(vfs, new Dir(file_or_dir), include_hidden: false, recursive: true))
                {
                    yield return f;
                }
            }
        }
    }

    public static IEnumerable<Dir> FoldersInPitchfork(Dir root)
        => PitchforkFolders
            .Select(root.GetDir);

    public static IEnumerable<Fil> FilesInPitchfork(Vfs vfs, Dir root, bool include_hidden)
        => FoldersInPitchfork(root)
            .Where(d => d.Exists(vfs))
            .SelectMany(d => IterateFiles(vfs, d, include_hidden, true));

    public static string GetFirstFolder(Dir root, Fil file)
     => root.RelativeFromTo(file).Split(Path.DirectorySeparatorChar, 2)[0];


    // iterate all files, ignores special folders
    public static IEnumerable<Fil> IterateFiles(Vfs vfs, Dir root, bool include_hidden, bool recursive)
    {
        // todo(Gustav): mvoe to Dir and remove recursive argument, Dir already has EnumerateFiles
        Debug.Assert(include_hidden == false); // todo(Gustav): remove argument
        return sub_iterate_files(vfs, root, recursive);

        static IEnumerable<Fil> sub_iterate_files(
            Vfs vfs, Dir root, bool include_directories)
        {
            foreach (var f in root.EnumerateFiles(vfs))
            {
                yield return f;
            }

            if (include_directories)
            {
                var files = root.EnumerateDirectories(vfs)
                        .Where(d => d.Name switch
                        {
                            // todo(Gustav): parse and use gitignore instead of hacky names
                            ".git" or "node_modules"
                            or "external" or "build"
                                => false,
                            _ => true,
                        })
                        .SelectMany(d => sub_iterate_files(vfs, d, true))
                    ;
                foreach (var f in files)
                {
                    yield return f;
                }
            }
        }
    }

    public static bool LooksAutoGenerated(IEnumerable<string> lines)
    => lines
        .Take(5)
        .Select(line => line.ToLowerInvariant())
        .Any(lower => lower.Contains("auto-generated") || lower.Contains("generated by"))
    ;

    internal static FileOrDir RealPath(FileOrDir rel)
    {
        // todo(Gustav): remove function
        return rel;
    }

    public static bool FileIsInFolder(Fil file, Dir folder)
        => file.Path.StartsWith(folder.Path);

    public static string RootPath(Dir root, string s)
        => Path.IsPathFullyQualified(s) ? s : Path.Join(root.Path, s);

    public static string RemoveCurrentDirDotFromDir(string s)
        => Path.GetFullPath(new DirectoryInfo(s).FullName);

    public static string RemoveCurrentDirDotFromFile(string s)
        => Path.GetFullPath(new FileInfo(s).FullName);
}

