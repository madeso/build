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
    public static IEnumerable<CompoundDef> AllClasses(DoxygenType parsed)
    {
        return parsed.compounds
            .Where(x => x.kind == Doxygen.Index.CompoundKind.Struct
            || x.kind == Doxygen.Index.CompoundKind.Class
            || x.kind == Doxygen.Index.CompoundKind.Interface
            )
            .Select(x => x.DoxygenFile.FirstCompound);
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

    internal static IEnumerable<memberdefType> AllMembersForAClass(CompoundDef k)
    {
        return k.SectionDefs
            .SelectMany(x => x.memberdef);
    }

    internal static string MemberToString(memberdefType it)
    {
        if (it.Kind == DoxMemberKind.Function)
        {
            return $"{it.Type} {it.Name}{it.Argsstring}";
        }
        return $"{it.Type} {it.Name}";
    }

    internal static IEnumerable<memberdefType> AllMembersInNamespace(CompoundDef ns, params DoxSectionKind[] kind)
    {
        var kinds = kind.ToImmutableHashSet();
        return ns.SectionDefs
                        .Where(s => kinds.Contains(s.kind))
                        .SelectMany(s => s.memberdef);
    }

    internal static IEnumerable<CompoundDef> IterateClassesInNamespace(DoxygenType dox, CompoundDef ns)
    {
        foreach (var kr in ns.InnerClasses)
        {
            yield return dox.refidLookup[kr.refid].DoxygenFile.FirstCompound;
        }
    }

    internal static IEnumerable<CompoundDef> IterateNamespacesInNamespace(DoxygenType dox, CompoundDef ns)
    {
        foreach (var kr in ns.InnerNamespaces)
        {
            yield return dox.refidLookup[kr.refid].DoxygenFile.FirstCompound;
        }
    }

    internal static IEnumerable<CompoundDef> IterateAllNamespaces(DoxygenType dox, CompoundDef rootNamespace)
    {
        var queue = new Queue<string>();
        queue.Enqueue(rootNamespace.Id);

        while (queue.Count > 0)
        {
            var ns = dox.refidLookup[queue.Dequeue()];
            foreach (var r in ns.DoxygenFile.FirstCompound.InnerNamespaces) queue.Enqueue(r.refid);

            yield return ns.DoxygenFile.FirstCompound;
        }
    }

    internal static IEnumerable<CompoundDef> AllNamespaces(DoxygenType dox)
    {
        return dox.compounds
            .Where(c => c.kind == CompoundKind.Namespace)
            .Select(ns => ns.DoxygenFile.FirstCompound);
    }

    internal static CompoundDef? FindNamespace(DoxygenType dox, string namespaceName)
    {
        return dox.compounds
            .Where(c => c.kind == CompoundKind.Namespace)
            .Where(c => c.Name == namespaceName)
            .FirstOrDefault()
            ?.DoxygenFile
            ?.FirstCompound
            ;
    }
}
