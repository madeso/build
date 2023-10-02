using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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

    public static string LocationToString(locationType? loc, string root)
    {
        if(loc == null)
        {
            return "<missing location>";
        }
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

    internal static IEnumerable<Doxygen.Compound.memberdefType> AllMembersInNamespace(Doxygen.Compound.compounddefType ns, params DoxSectionKind[] kind)
    {
        var kinds = kind.ToImmutableHashSet();
        return ns.Sectiondef
                        .Where(s => kinds.Contains(s.kind))
                        .SelectMany(s => s.memberdef);
    }

    internal static IEnumerable<CompoundType> IterateClassesInNamespace(Doxygen.Index.DoxygenType dox, Doxygen.Compound.compounddefType ns)
    {
        foreach (var kr in ns.Innerclass)
        {
            yield return dox.refidLookup[kr.refid];
        }
    }

    internal static IEnumerable<Doxygen.Compound.compounddefType> IterateNamespaces(Doxygen.Index.DoxygenType dox, CompoundType rootNamespace)
    {
        var queue = new Queue<string>();
        queue.Enqueue(rootNamespace.refid);

        while (queue.Count > 0)
        {
            var ns = dox.refidLookup[queue.Dequeue()];
            foreach (var r in ns.Compund.Compound.Innernamespace) queue.Enqueue(r.refid);

            yield return ns.Compund.Compound;
        }
    }

    internal static IEnumerable<Doxygen.Compound.compounddefType> AllNamespaces(Doxygen.Index.DoxygenType dox)
    {
        return dox.compounds
            .Where(c => c.kind == Doxygen.Index.CompoundKind.Namespace)
            .Select(ns => ns.Compund.Compound);
    }

    internal static CompoundType? FindNamespace(Doxygen.Index.DoxygenType dox, string namespaceName)
    {
        return dox.compounds
            .Where(c => c.kind == Doxygen.Index.CompoundKind.Namespace)
            .Where(c => c.name == namespaceName)
            .FirstOrDefault();
    }
}
