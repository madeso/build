using System.Collections.Immutable;
using Workbench.Shared.Extensions;

namespace Workbench.Shared;

public class UniqueFiles
{
    private readonly List<Fil> paths = new();
    
    public void Add(Fil path)
    {
        paths.Add(path);
    }

    public Dir? GetCommon()
    {
        var more = paths.Select(f => f.Directory).IgnoreNull().ToImmutableArray();
        if (more.Length == 0) return null;
        
        var common = more[0];
        foreach (var dir in more.Skip(1))
        {
            common = GetCommonPrefix(common, dir);
            if (common == null) return null;
        }

        return common;
    }

    private static Dir? GetCommonPrefix(Dir left, Dir right)
    {
        var common = split(left)
            .ZipLongest(split(right))
            .LastOrDefault(pair => pair.Item1?.Equals(pair.Item2) ?? false).Item1;
        return common;

        // with c:\foo\bar\ returns c:\ c:\foo\ and c:\foo\bar\
        static IEnumerable<Dir> split(Dir dir)
        {
            List<Dir> ret = new();
            var c = dir;
            while (c != null)
            {
                ret.Add(c);
                c = c.Parent;
            }

            ret.Reverse();
            return ret;
        }
    }
}
