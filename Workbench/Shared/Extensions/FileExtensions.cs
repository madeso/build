namespace Workbench.Shared.Extensions;

public static class FileExtensions
{
    public static DirectoryInfo GetDir(this DirectoryInfo dir, string sub)
    {
        return new DirectoryInfo(Path.Join(dir.FullName, sub));
    }

    public static DirectoryInfo GetSubDirs(this DirectoryInfo dir, IEnumerable<string> sub)
    {
        return sub.Aggregate(dir, (current, name) => current.GetDir(name));
    }

    public static DirectoryInfo GetSubDirs(this DirectoryInfo dir, params string[] sub)
    {
        return sub.Aggregate(dir, (current, name) => current.GetDir(name));
    }

    public static FileInfo GetFile(this DirectoryInfo dir, string file)
    {
        return new FileInfo(Path.Join(dir.FullName, file));
    }

    public static bool HasFile(this DirectoryInfo build_folder, string x)
    {
        return Path.GetRelativePath(build_folder.FullName, x).StartsWith("..") == false;
    }

    public static bool HasAnyExtension(this FileInfo file, string[] extensions)
    {
        return FileUtil.FileHasAnyExtension(file.FullName, extensions);
    }
}