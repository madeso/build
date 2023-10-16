using System.Collections.Immutable;

namespace Workbench.Utils;

public class UniqueFiles
{
    private readonly List<FileInfo> paths = new();
    
    public void Add(string path)
    {
        paths.Add(new FileInfo(path));
    }

    public string? GetCommon()
    {
        var more = paths.Select(f => f.Directory).IgnoreNull().ToImmutableArray();
        if (more.Length == 0) return null;
        
        var common = more[0];
        foreach (var dir in more.Skip(1))
        {
            common = GetCommonPrefix(common, dir);
            if (common == null) return null;
        }

        return common.FullName;
    }

    private static DirectoryInfo? GetCommonPrefix(DirectoryInfo left, DirectoryInfo right)
    {
        var common = split(left)
            .ZipLongest(split(right))
            .LastOrDefault(pair => pair.Item1 == pair.Item2).Item1;
        return common != null ? new DirectoryInfo(common) : null;

        static IEnumerable<string> split(DirectoryInfo dir)
        {
            List<string> ret = new();
            var c = dir;
            while (c != null)
            {
                ret.Add(c.FullName);
                c = c.Parent;
            }

            ret.Reverse();
            return ret;
        }
    }
}
