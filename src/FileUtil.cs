namespace Workbench;

internal static class FileUtil
{
    public static readonly string[] HEADER_FILES = new string[] { "", ".h", ".hpp", ".hxx" };
    public static readonly string[] SOURCE_FILES = new string[] { ".cc", ".cpp", ".cxx", ".inl" };

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
}