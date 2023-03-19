namespace Workbench.Utils;

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

    public static bool HasFile(this DirectoryInfo buildFolder, string x)
    {
        return Path.GetRelativePath(buildFolder.FullName, x).StartsWith("..") == false;
    }
}
