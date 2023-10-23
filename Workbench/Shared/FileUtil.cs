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
    JsonConfig
}

internal static class FileUtil
{
    internal static bool IsHeaderOrSource(FileInfo path)
        => IsHeader(path) || IsSource(path);

    internal static bool IsSource(FileInfo path)
        => ClassifySource(path) == Language.CppSource;

    internal static bool IsHeader(FileInfo path)
        => ClassifySource(path) == Language.CppHeader;

    public static bool IsTranslationUnit(FileInfo path)
        => ClassifySource(path) is Language.CppSource or Language.ObjectiveCpp;

    public static Language ClassifySource(FileInfo f)
        => f.Extension switch
        {
            ".cs" => Language.CSharp,
            ".tsx" or ".jsx" => Language.React,
            ".js" => Language.Javascript,
            ".ts" => Language.Typescript,

            ".jsonc" => Language.JsonConfig,

            ".cc" or ".cpp" or ".c" or ".cxx" => Language.CppSource,
            ".hh" or ".hpp" or ".h" or ".hxx" or ".inl" or "" => Language.CppHeader,

            ".mm" or ".m" => Language.ObjectiveCpp,
            _ => Language.Unknown,
        };

    public static string? ClassifySourceOrNull(FileInfo f)
        => ClassifySource(f) switch
        {
            Language.CSharp => "C#",
            Language.React => "React",
            Language.Typescript => "JavaScript",
            Language.Javascript => "TypeScript",
            Language.JsonConfig => "JSON config",
            Language.CppSource or Language.CppHeader => "C/C++",
            Language.ObjectiveCpp => "Objective-C/C++",
            Language.Unknown => null,
            // why is a invalid enum value a valid value???
            _ => throw new ArgumentOutOfRangeException()
        };

    public static readonly string[] PitchforkFolders = { "apps", "libs", "src", "include" };

    public static IEnumerable<string> PitchforkBuildFolders(string root)
    {
        var build = new DirectoryInfo(Path.Join(root, "build"));
        if (!build.Exists)
        {
            yield break;
        }

        yield return build.FullName;

        foreach (var d in build.GetDirectories())
        {
            yield return d.FullName;
        }
    }

    public static IEnumerable<string> SourcesFromArgs(IEnumerable<string> args, Func<FileInfo, bool> theese_files)
        => ListFilesFromArgs(args)
            .Where(theese_files)
            .Select(f => f.FullName);

    public static IEnumerable<FileInfo> ListFilesFromArgs(IEnumerable<string> args)
    {
        foreach (var file_or_dir in args)
        {
            if (File.Exists(file_or_dir)) yield return new FileInfo(file_or_dir);
            else // assume directory
            {
                foreach (var f in IterateFiles(new DirectoryInfo(file_or_dir), include_hidden: false, recursive: true))
                {
                    yield return f;
                }
            }
        }
    }

    public static IEnumerable<DirectoryInfo> FoldersInPitchfork(DirectoryInfo root)
        => PitchforkFolders
            .Select(relative_dir => new DirectoryInfo(Path.Join(root.FullName, relative_dir)));

    public static IEnumerable<FileInfo> FilesInPitchfork(DirectoryInfo root, bool include_hidden)
        => FoldersInPitchfork(root)
            .Where(d => d.Exists)
            .SelectMany(d => IterateFiles(d, include_hidden, true));

    public static string GetFirstFolder(string root, string file)
     => Path.GetRelativePath(root, file)
         .Split(Path.DirectorySeparatorChar, 2)[0];


    // iterate all files, ignores special folders
    public static IEnumerable<FileInfo> IterateFiles(DirectoryInfo root, bool include_hidden, bool recursive)
    {
        var search_options = new EnumerationOptions
        {
            AttributesToSkip = include_hidden
                ? FileAttributes.Hidden | FileAttributes.System
                : FileAttributes.System
        };

        return sub_iterate_files(root, search_options, recursive);

        static IEnumerable<FileInfo> sub_iterate_files(
            DirectoryInfo root, EnumerationOptions search_options, bool include_directories)
        {
            foreach (var f in root.GetFiles("*", search_options))
            {
                yield return f;
            }

            if (include_directories)
            {
                var files = root.GetDirectories("*", search_options)
                        .Where(d => d.Name switch
                        {
                            // todo(Gustav): parse and use gitignore instead of hacky names
                            ".git" or "node_modules"
                            or "external" or "build"
                                => false,
                            _ => true,
                        })
                        .SelectMany(d => sub_iterate_files(d, search_options, true))
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

    internal static string RealPath(string rel)
    {
        var dir = new DirectoryInfo(rel);
        if (dir.Exists) { return dir.FullName; }

        var file = new FileInfo(rel);
        if (file.Exists) { return file.FullName; }

        return rel;
    }

    public static bool FileIsInFolder(string file, string folder)
        => new FileInfo(file).FullName.StartsWith(new DirectoryInfo(folder).FullName);
}
