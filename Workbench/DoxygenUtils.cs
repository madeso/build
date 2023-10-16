using System.Collections.Immutable;
using Workbench.Doxygen.Compound;
using Workbench.Doxygen.Index;

namespace Workbench;

internal static class DoxygenUtils
{
    public static IEnumerable<CompoundDef> AllClasses(DoxygenType parsed)
    {
        return parsed.Compounds
            .Where(x => x.Kind == CompoundKind.Struct
            || x.Kind == CompoundKind.Class
            || x.Kind == CompoundKind.Interface
            )
            .Select(x => x.DoxygenFile.FirstCompound);
    }

    internal static string DoxygenFileToPath(LocationType loc, string root)
    {
        var abs = new FileInfo(Path.Join(root, loc.File)).FullName;
        var print = File.Exists(abs) ? abs : loc.File;
        return print;
    }

    public static string LocationToString(LocationType? loc, string root)
    {
        if(loc == null)
        {
            return "<missing location>";
        }
        var file_name = DoxygenFileToPath(loc, root);
        return Printer.ToFileString(file_name, loc.Line ?? -1);
    }

    public static bool IsConstructorOrDestructor(MemberDefinitionType m)
    {
        var ret = m.Type?.Nodes;
        if (ret == null) { return true; }

        // exclude "constexpr" return values
        var rets = ret.Where(node => !(node is LinkedTextType.Text text && is_keyword(text))).ToArray();
        var ret_count = rets.Count();

        // Console.WriteLine($"ret detection: {m.Name} -- {retCount}: {ret}");
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

    internal static IEnumerable<MemberDefinitionType> AllMembersForAClass(CompoundDef k)
    {
        return k.SectionDefs
            .SelectMany(x => x.MemberDef);
    }

    internal static string MemberToString(MemberDefinitionType it)
    {
        if (it.Kind == DoxMemberKind.Function)
        {
            return $"{it.Type} {it.Name}{it.ArgsString}";
        }
        return $"{it.Type} {it.Name}";
    }

    internal static IEnumerable<MemberDefinitionType> AllMembersInNamespace(CompoundDef ns, params DoxSectionKind[] kind)
    {
        var kinds = kind.ToImmutableHashSet();
        return ns.SectionDefs
                        .Where(s => kinds.Contains(s.Kind))
                        .SelectMany(s => s.MemberDef);
    }

    internal static IEnumerable<CompoundDef> IterateClassesInNamespace(DoxygenType dox, CompoundDef ns)
        => ns.InnerClasses
            .Select(kr => dox.refidLookup[kr.RefId].DoxygenFile.FirstCompound);

    internal static IEnumerable<CompoundDef> IterateNamespacesInNamespace(DoxygenType dox, CompoundDef ns)
        => ns.InnerNamespaces
            .Select(kr => dox.refidLookup[kr.RefId].DoxygenFile.FirstCompound);

    internal static IEnumerable<CompoundDef> IterateAllNamespaces(DoxygenType dox, CompoundDef root_namespace)
    {
        var queue = new Queue<string>();
        queue.Enqueue(root_namespace.Id);

        while (queue.Count > 0)
        {
            var ns = dox.refidLookup[queue.Dequeue()];
            foreach (var r in ns.DoxygenFile.FirstCompound.InnerNamespaces) queue.Enqueue(r.RefId);

            yield return ns.DoxygenFile.FirstCompound;
        }
    }

    internal static IEnumerable<CompoundDef> AllNamespaces(DoxygenType dox)
    {
        return dox.Compounds
            .Where(c => c.Kind == CompoundKind.Namespace)
            .Select(ns => ns.DoxygenFile.FirstCompound);
    }

    internal static CompoundDef? FindNamespace(DoxygenType dox, string namespace_name)
    {
        return dox.Compounds
                .Where(c => c.Kind == CompoundKind.Namespace)
                .FirstOrDefault(c => c.Name == namespace_name)
                ?.DoxygenFile
                ?.FirstCompound
            ;
    }
}
