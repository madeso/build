using System.Collections.Immutable;
using Workbench.Shared.Doxygen.Compound;
using Workbench.Shared.Doxygen.Index;

namespace Workbench.Shared.Doxygen;

internal static class DoxygenUtils
{
    /*    
    public static IEnumerable<CompoundDef> AllClasses(DoxygenType parsed)
    {
        return parsed.Compounds
            .Where(x => x.Kind == Index.CompoundKind.@struct
            || x.Kind == Index.CompoundKind.@class
            || x.Kind == Index.CompoundKind.@interface
            )
            .Select(x => x.DoxygenFile.FirstCompound);
    }

    internal static Fil DoxygenFileToPath(Vfs vfs, LocationType loc, Dir root)
    {
        var abs = root.GetFile(loc.File);
        var print = abs.Exists(vfs) ? abs : new Fil(loc.File);
        return print;
    }

    public static FileLine? LocationToString(Vfs vfs, LocationType? loc, Dir root)
    {
        if (loc == null)
        {
            return null;
        }
        var file_name = DoxygenFileToPath(vfs, loc, root);
        return new FileLine(file_name, loc.Line);
    }

    public static bool IsConstructorOrDestructor(MemberDefinitionType m)
    {
        var ret = m.Type?.Nodes;
        if (ret == null) { return true; }

        // exclude "constexpr" return values
        var rets = ret.Where(node => !(node is LinkedTextType.Text text && is_keyword(text))).ToArray();
        var ret_count = rets.Length;

        return ret_count == 0;

        static bool is_keyword(LinkedTextType.Text text)
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

    internal static bool IsFunctionOverride(MemberDefinitionType m)
    {
        return m.ArgsString?.EndsWith("override") ?? false;
    }

    internal static string MemberToString(MemberDefinitionType it)
    {
        if (it.Kind == DoxMemberKind.Function)
        {
            return $"{it.Type} {it.Name}{it.ArgsString}";
        }
        return $"{it.Type} {it.Name}";
    }

    internal static IEnumerable<CompoundDef> IterateNamespacesInNamespace(DoxygenType dox, CompoundDef ns)
        => ns.InnerNamespaces
            .Select(kr => dox.refidLookup[kr.RefId].DoxygenFile.FirstCompound);

    internal static CompoundDef? FindNamespace(DoxygenType dox, string namespace_name)
    {
        // todo(Gustav): merge with AllNamespaces above
        return dox.Compounds
                .Where(c => c.Kind == CompoundKind.Namespace)
                .FirstOrDefault(c => c.Name == namespace_name)
                ?.DoxygenFile
                .FirstCompound
            ;
    }
    */
    public static IEnumerable<memberdefType> AllMembersForAClass(compounddefType k)
    {
        return k.sectiondef
            .SelectMany(x => x.memberdef);
    }

    public static IEnumerable<compounddefType> AllNamespaces(DoxygenType dox)
    {
        return dox.Compounds
            .Where(c => c.Kind == CompoundKind.@namespace)
            .Select(ns => ns.DoxygenFile.FirstCompound);
    }

    public static compounddefType? FindNamespace(DoxygenType dox, string namespace_name)
    {
        // todo(Gustav): merge with AllNamespaces above
        return dox.Compounds
                .Where(c => c.Kind == CompoundKind.@namespace)
                .FirstOrDefault(c => c.Name == namespace_name)
                ?.DoxygenFile
                .FirstCompound
            ;
    }

    internal static IEnumerable<compounddefType> IterateAllNamespaces(DoxygenType dox, compounddefType root_namespace)
    {
        var queue = new Queue<string>();
        queue.Enqueue(root_namespace.id);

        while (queue.Count > 0)
        {
            var ns = dox.refidLookup[queue.Dequeue()];
            foreach (var r in ns.DoxygenFile.FirstCompound.innernamespace) queue.Enqueue(r.refid);

            yield return ns.DoxygenFile.FirstCompound;
        }
    }

    internal static IEnumerable<compounddefType> IterateClassesInNamespace(DoxygenType dox, compounddefType ns)
        => ns.innerclass
            .Select(kr => dox.refidLookup[kr.refid].DoxygenFile.FirstCompound);

    internal static IEnumerable<memberdefType> AllMembersInNamespace(compounddefType ns, params DoxSectionKind[] kind)
    {
        var kinds = kind.ToImmutableHashSet();
        return ns.sectiondef
            .Where(s => kinds.Contains(s.kind))
            .SelectMany(s => s.memberdef);
    }
}
