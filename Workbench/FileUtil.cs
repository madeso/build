﻿namespace Workbench;

internal static class FileUtil
{
    public static readonly string[] HEADER_FILES = new string[] { "", ".h", ".hpp", ".hxx" };
    public static readonly string[] SOURCE_FILES = new string[] { ".cc", ".cpp", ".cxx", ".inl" };
    public static readonly string[] PITCHFORK_FOLDERS = new string[] { "apps", "libs", "src", "include" };

public static bool IsTranslationUnitExtension(string ext)
    {
        return ext switch
        {
            ".cpp" or ".c" or ".cc" or ".cxx" or ".mm" or ".m" => true,
            _ => false,
        };
    }

    internal static bool IsSource(string path)
    {
        return Path.GetExtension(path) switch
        {
            ".cc" or ".cpp" or ".c" => true,
            _ => false
        };
    }

    internal static bool is_header(string path)
    {
        return Path.GetExtension(path) switch
        {
            "" => true,
            ".h" => true,
            ".hpp" => true,
            _ => false
        };
    }

    public static bool IsTranslationUnit(string path)
    {
        return IsTranslationUnitExtension(Path.GetExtension(path));
    }

    public static bool FileHasAnyExtension(string filePath, string[] extensions)
    {
        var ext = Path.GetExtension(filePath);
        return extensions.Contains(ext);
    }

    public static IEnumerable<string> ListFilesRecursivly(string path, string[] extensions)
    {
        foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            bool x = Workbench.FileUtil.FileHasAnyExtension(f, extensions);
            if (x)
            {
                yield return f;
            }
        }
    }

    public static string GetFirstFolder(string root, string file)
    {
        var rel = Path.GetRelativePath(root, file);
        var cat = rel.Split(Path.DirectorySeparatorChar, 2)[0];
        return cat;
    }

    public static IEnumerable<FileInfo> IterateFiles(DirectoryInfo root, bool includeHidden, bool recursive)
    {
        var searchOptions = new EnumerationOptions
        {
            AttributesToSkip = includeHidden
                ? FileAttributes.Hidden | FileAttributes.System
                : FileAttributes.System
        };

        foreach (var f in SubIterateFiles(root, searchOptions, recursive))
        {
            yield return f;
        }

        static IEnumerable<FileInfo> SubIterateFiles(DirectoryInfo root, EnumerationOptions searchOptions, bool includeDirectories)
        {
            foreach (var f in root.GetFiles("*", searchOptions))
            {
                yield return f;
            }

            if (includeDirectories)
            {
                foreach (var d in root.GetDirectories("*", searchOptions))
                {
                    if (IsValidDirectory(d) == false) { continue; }
                    foreach (var f in SubIterateFiles(d, searchOptions, true))
                    {
                        yield return f;
                    }
                }
            }
        }
    }

    public static string? ClassifySourceOrNull(FileInfo f)
    {
        return f.Extension switch
        {
            ".cs" => "c#",
            ".jsx" => "React",
            ".ts" or ".js" => "Javascript/typescript",
            ".cpp" or ".c" or ".h" or ".hpp" => "C/C++",
            _ => null,
        };
    }

    public static bool LooksAutoGenerated(IEnumerable<string> lines)
    {
        return lines
                    .Take(5)
                    .Select(x => LineLooksLikeAutoGenerated(x))
                    .Where(x => x)
                    .FirstOrDefault(false);
    }

    private static bool LineLooksLikeAutoGenerated(string line)
    {
        var lower = line.ToLowerInvariant();
        if (lower.Contains("auto-generated"))
        {
            return true;
        }

        if (lower.Contains("generated by"))
        {
            return true;
        }

        return false;
    }

    private static bool IsValidDirectory(DirectoryInfo d)
    {
        return d.Name switch
        {
            ".git" => false,
            "node_modules" => false,
            _ => true,
        };
    }
}