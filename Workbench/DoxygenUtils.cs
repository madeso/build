using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Workbench.Doxygen.Compound;
using Workbench.Doxygen.Index;

namespace Workbench;

internal static class DoxygenUtils
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
        string fileName = DoxygenUtils.DoxygenFileToPath(loc, root);
        return Printer.ToFileString(fileName, loc.line ?? -1);
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

    internal static IEnumerable<memberdefType> AllMethodsInClass(CompoundType k)
    {
        return k.Compund.Compound.Sectiondef.SelectMany(x => x.memberdef);
    }

    internal static string MemberToString(memberdefType it)
    {
        if (it.Kind == DoxMemberKind.Function)
        {
            return $"{it.Type} {it.Name}{it.Argsstring}";
        }
        return $"{it.Type} {it.Name}";
    }
}
