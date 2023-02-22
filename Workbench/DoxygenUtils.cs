using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Workbench.Doxygen.Compound;
using Workbench.Doxygen.Index;

namespace Workbench;

internal class DoxygenUtils
{
    public static IEnumerable<CompoundType> AllClasses(Doxygen.Index.DoxygenType parsed)
    {
        return parsed.compounds.Where(x => x.kind == Doxygen.Index.CompoundKind.Struct || x.kind == Doxygen.Index.CompoundKind.Class);
    }

    internal static string DoxygenFileToPath(locationType loc, string root)
    {
        var abs = new FileInfo(Path.Join(root, loc.file)).FullName;
        var print = File.Exists(abs) ? abs : loc.file;
        return print;
    }

    public static string LocationToString(locationType loc, string root)
    {
        string print = DoxygenUtils.DoxygenFileToPath(loc, root);
        return $"{print}({loc.line})";
    }

    public static bool IsConstructorOrDestructor(memberdefType m)
    {
        var ret = m.Type?.Nodes;
        if (ret == null) { return true; }

        // exclude "constexpr" return values
        var rets = ret.Where(node => !(node is linkedTextType.Text text && IsKeyword(text))).ToArray();
        var retCount = rets.Count();

        // Console.WriteLine($"ret detection: {m.Name} -- {retCount}: {ret}");
        return retCount == 0;

        static bool IsKeyword(linkedTextType.Text text)
        {
            return text.Value.Trim() switch
            {
                "constexpr" => true,
                "const" => true,
                "&" => true,
                _ => false,
            };
        }
    }

    internal static bool IsFunctionOverride(memberdefType m)
    {
        return m.Argsstring?.EndsWith("override") ?? false;
    }
}
