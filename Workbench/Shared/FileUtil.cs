using System.Diagnostics;
using Workbench.Shared.Extensions;

namespace Workbench.Shared;

public enum Language
{
    CSharp,
    React,
    Typescript,
    Javascript,
    CppSource,
    CppHeader,
    Unknown,
    ObjectiveCpp,
    JsonConfig,
    Swift,
    CMake
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
            ".tsx" or ".jsx" => Language.React,
            ".js" => Language.Javascript,
            ".ts" => Language.Typescript,
            ".swift" => Language.Swift,

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
            Language.React => "React",
            Language.Typescript => "JavaScript",
            Language.Javascript => "TypeScript",
            Language.JsonConfig => "JSON config",
            Language.Swift => "Swift",
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
            Language.React => "React",
            Language.Typescript => "JavaScript",
            Language.Javascript => "TypeScript",
            Language.JsonConfig => "JSON config",
            Language.Swift => "Swift",
            Language.CppSource => "C/C++ Source",
            Language.CppHeader => "C/C++ Header",
            Language.ObjectiveCpp => "Objective-C/C++",
            Language.CMake => "CMake",
            Language.Unknown => "Unknown",
            // why is a invalid enum value a valid value???
            _ => throw new ArgumentOutOfRangeException()
        };

    public static readonly string[] PitchforkFolders = { "apps", "libs", "src", "include" };

    public static IEnumerable<Dir> PitchforkBuildFolders(Dir root)
    {
        var build = root.GetDir("build");
        if (!build.Exists)
        {
            yield break;
        }

        yield return build;

        foreach (var d in build.EnumerateDirectories())
        {
            yield return d;
        }
    }

    public static IEnumerable<Fil> SourcesFromArgs(IEnumerable<string> args, Func<Fil, bool> these_files)
        => ListFilesFromArgs(args)
            .Where(these_files);

    public static IEnumerable<Fil> ListFilesFromArgs(IEnumerable<string> args)
    {
        foreach (var unrooted_file_or_dir in args)
        {
            var file_or_dir = RootPath(Dir.CurrentDirectory, unrooted_file_or_dir);
            if (File.Exists(file_or_dir)) yield return new Fil(file_or_dir);
            else // assume directory
            {
                foreach (var f in IterateFiles(new Dir(file_or_dir), include_hidden: false, recursive: true))
                {
                    yield return f;
                }
            }
        }
    }

    public static IEnumerable<Dir> FoldersInPitchfork(Dir root)
        => PitchforkFolders
            .Select(root.GetDir);

    public static IEnumerable<Fil> FilesInPitchfork(Dir root, bool include_hidden)
        => FoldersInPitchfork(root)
            .Where(d => d.Exists)
            .SelectMany(d => IterateFiles(d, include_hidden, true));

    public static string GetFirstFolder(Dir root, Fil file)
     => root.RelativeFromTo(file).Split(Path.DirectorySeparatorChar, 2)[0];


    // iterate all files, ignores special folders
    public static IEnumerable<Fil> IterateFiles(Dir root, bool include_hidden, bool recursive)
    {
        // todo(Gustav): mvoe to Dir and remove recursive argument, Dir already has EnumerateFiles
        Debug.Assert(include_hidden == false); // todo(Gustav): remove argument
        return sub_iterate_files(root, recursive);

        static IEnumerable<Fil> sub_iterate_files(
            Dir root, bool include_directories)
        {
            foreach (var f in root.EnumerateFiles())
            {
                yield return f;
            }

            if (include_directories)
            {
                var files = root.EnumerateDirectories()
                        .Where(d => d.Name switch
                        {
                            // todo(Gustav): parse and use gitignore instead of hacky names
                            ".git" or "node_modules"
                            or "external" or "build"
                                => false,
                            _ => true,
                        })
                        .SelectMany(d => sub_iterate_files(d, true))
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
}

