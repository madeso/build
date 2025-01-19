using Spectre.Console;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Workbench.Shared;
using Workbench.Shared.Doxygen;

namespace Workbench.Commands.Dependencies;

public static class Dependencies
{
    const string NO_NAMESPACE = "|";

    internal static async Task WriteToGraphvizAsync(Vfs vfs, Config.Paths paths, Dir cwd, Log log, Dir doxygen_xml, string namespace_name,
        Fil output_file, ImmutableHashSet<string> ignored_classes, bool include_functions, bool add_arguments,
        bool add_members, bool cluster_namespace)
    {
        AnsiConsole.WriteLine("Parsing doxygen XML...");
        var dox = Doxygen.ParseIndex(doxygen_xml);
        var root_namespace = namespace_name == NO_NAMESPACE ? null : DoxygenUtils.FindNamespace(dox, namespace_name);

        if (namespace_name != NO_NAMESPACE && root_namespace == null)
        {
            log.Error($"Unknown namespace {namespace_name}");
            return;
        }

        AnsiConsole.WriteLine("Working...");
        var namespaces = (
                root_namespace == null
                ? DoxygenUtils.AllNamespaces(dox)
                : DoxygenUtils.IterateAllNamespaces(dox, root_namespace)
            ).ToImmutableArray();

        var g = new Graphviz();
        var classes = new Dictionary<string, Graphviz.Node>();

        AnsiConsole.WriteLine("Adding classes...");
        foreach (var ns in namespaces)
        {
            foreach (var k in DoxygenUtils.IterateClassesInNamespace(dox, ns))
            {
                if (ignored_classes.Contains(k.CompoundName))
                {
                    continue;
                }

                var name = cluster_namespace
                    ? k.CompoundName.Split(":", StringSplitOptions.RemoveEmptyEntries).Last()
                    : k.CompoundName;

                var node = g.AddNodeWithId(name, Shape.Box3d, k.Id);
                classes.Add(k.Id, node);

                node.Cluster = g.FindOrCreateCluster(ns.CompoundName);
            }
        }

        AnsiConsole.WriteLine("Adding typedefs...");
        foreach (var k in namespaces.SelectMany(ns => DoxygenUtils.AllMembersInNamespace(ns, DoxSectionKind.Typedef)))
        {
            if (ignored_classes.Contains(k.Name))
            {
                continue;
            }

            var node = g.AddNode(k.Name, Shape.Box);
            classes.Add(k.Id, node);

            var existing_refs = new HashSet<string>();
            AddTypeLink(g, classes, () => node, existing_refs, k.Type);
        }

        AnsiConsole.WriteLine("Adding members for class...");
        foreach (var klass in namespaces.SelectMany(ns => DoxygenUtils.IterateClassesInNamespace(dox, ns)))
        {
            if (false == classes.TryGetValue(klass.Id, out var graphviz_klass))
            {
                continue;
            }

            var existing_refs = new HashSet<string>();

            // add inheritance
            foreach (var r in klass.BaseCompoundRefs)
            {
                if (r.RefId == null) continue;
                AddReference(r.RefId, g, classes, () => graphviz_klass, existing_refs);
            }

            if (!add_members) { continue; }

            foreach (var member in DoxygenUtils.AllMembersForAClass(klass))
            {
                if (include_functions == false && member.Kind == DoxMemberKind.Function)
                {
                    continue;
                }

                if (add_arguments)
                {
                    foreach (var p in member.Param)
                    {
                        AddTypeLink(g, classes, () => graphviz_klass, existing_refs, p.Type);
                    }
                }

                // return value for a function or the type of the member
                AddTypeLink(g, classes, () => graphviz_klass, existing_refs, member.Type);
            }
        }

        if (include_functions)
        {
            AnsiConsole.WriteLine("Adding functions...");
            foreach (var func in namespaces.SelectMany(ns => DoxygenUtils.AllMembersInNamespace(ns, DoxSectionKind.Func)))
            {
                var existing_refs = new HashSet<string>();

                Graphviz.Node? func_node = null;

                if (add_arguments) 
                {
                    foreach (var p in func.Param)
                    {
                        AddTypeLink(g, classes, get_func_node, existing_refs, p.Type);
                    }
                }

                AddTypeLink(g, classes, get_func_node, existing_refs, func.Type);
                continue;

                Graphviz.Node get_func_node()
                {
                    return func_node ??= g.AddNode($"{func.Name}{func.ArgsString}", Shape.Ellipse);
                }
            }
        }

        await g.SmartWriteFileAsync(vfs, paths, cwd, output_file, log);
    }

    internal static void PrintLists(Log log, Dir doxygen_xml, string namespace_name)
    {
        AnsiConsole.WriteLine("Parsing doxygen XML...");
        var dox = Doxygen.ParseIndex(doxygen_xml);
        var root_namespace = DoxygenUtils.FindNamespace(dox, namespace_name);

        if (root_namespace == null)
        {
            log.Error($"Unknown namespace {namespace_name}");
            return;
        }

        AnsiConsole.WriteLine("Working...");
        var namespaces = DoxygenUtils.IterateAllNamespaces(dox, root_namespace).ToImmutableArray();

        foreach (var k in namespaces.SelectMany(ns => DoxygenUtils.IterateClassesInNamespace(dox, ns)))
        {
            AnsiConsole.MarkupLineInterpolated($"Class {k.CompoundName}");
        }

        foreach (var k in namespaces.SelectMany(ns => DoxygenUtils.AllMembersInNamespace(ns, DoxSectionKind.Typedef)))
        {
            AnsiConsole.MarkupLineInterpolated($"Typedef {k.Name}");
        }

        foreach (var func in namespaces.SelectMany(ns => DoxygenUtils.AllMembersInNamespace(ns, DoxSectionKind.Func)))
        {
            AnsiConsole.MarkupLineInterpolated($"Func {func.Name}{func.ArgsString}");
        }
    }

    private static void AddTypeLink(Graphviz g, Dictionary<string, Graphviz.Node> valid_types,
        Func<Graphviz.Node> parent_func, HashSet<string> existing_refs, LinkedTextType? type)
    {
        if (type == null) { return; }

        Graphviz.Node? parent = null;
        foreach (var node in type.Nodes)
        {
            var re = node as LinkedTextType.Ref;
            if (re == null) continue;

            AddReference(re.Value.RefId, g, valid_types, () =>
                {
                    if (parent != null) return parent;
                    parent = parent_func();
                    return parent;
                }, existing_refs);
        }
    }

    private static void AddReference(string id, Graphviz g, Dictionary<string, Graphviz.Node> valid_types,
        Func<Graphviz.Node> parent_func, HashSet<string> existing_refs)
    {
        if (existing_refs.Contains(id)) return;
        existing_refs.Add(id);

        if (false == valid_types.TryGetValue(id, out var linked_klass)) return;

        g.AddEdge(parent_func(), linked_klass);
    }

    [TypeConverter(typeof(EnumTypeConverter<ClusterCallGraphOn>))]
    [JsonConverter(typeof(EnumJsonConverter<ClusterCallGraphOn>))]
    public enum ClusterCallGraphOn
    {
        [EnumString("none")]
        None,

        [EnumString("class")]
        Class,

        [EnumString("namespace")]
        Namespace
    }

    private record Method(CompoundDef? Klass, MemberDefinitionType Function);

    internal static async Task WriteCallGraphToGraphvizAsync(Vfs vfs, Config.Paths paths, Dir cwd, Log log, Dir doxygen_xml,
        Fil output_file, ClusterCallGraphOn cluster_on)
    {
        // todo(Gustav): option to remove namespace prefixes

        AnsiConsole.WriteLine("Parsing doxygen XML...");
        var dox = Doxygen.ParseIndex(doxygen_xml);

        AnsiConsole.WriteLine("Collecting functions...");
        var namespaces = DoxygenUtils.AllNamespaces(dox).ToImmutableArray();


        var member_functions = namespaces
            .SelectMany(ns => DoxygenUtils.IterateClassesInNamespace(dox, ns))
            .SelectMany(klass => DoxygenUtils.AllMembersForAClass(klass)
                .Select(fun => new Method(klass, fun))
            )
            // add properties too
            .Where(mem => mem.Function.Kind == DoxMemberKind.Function
                        || mem.Function.Kind == DoxMemberKind.Property)
            ;
        var free_functions =
            namespaces.SelectMany(ns => DoxygenUtils.AllMembersInNamespace(ns, DoxSectionKind.Func))
            .Where(fun => fun.Kind == DoxMemberKind.Function
                        || fun.Kind == DoxMemberKind.Property)
            .Select(fun => new Method(null, fun));

        var all_functions = member_functions.Concat(free_functions).ToImmutableArray();


        AnsiConsole.WriteLine("Adding functions...");
        var g = new Graphviz();
        var functions = new Dictionary<string, Graphviz.Node>();
        foreach (var func in all_functions)
        {
            var base_name = $"{func.Function.Name}{func.Function.ArgsString}";
            if (cluster_on != ClusterCallGraphOn.Class && func.Klass != null)
            {
                base_name = $"{func.Klass.CompoundName}.{base_name}";
            }
            functions.Add(func.Function.Id, g.AddNodeWithId(base_name, func_to_shape(func), func.Function.Id));
        }

        switch (cluster_on)
        {
            case ClusterCallGraphOn.Class:
                foreach (var klass in namespaces.SelectMany(ns => DoxygenUtils.IterateClassesInNamespace(dox, ns)))
                {
                    var cluster = g.FindOrCreateCluster(klass.Id, klass.CompoundName);
                    foreach (var fun in DoxygenUtils.AllMembersForAClass(klass)
                        .Where(mem => mem.Kind == DoxMemberKind.Function))
                    {
                        if (false == functions.TryGetValue(fun.Id, out var fun_node))
                        {
                            continue;
                        }

                        fun_node.Cluster = cluster;
                    }
                }
                break;
            case ClusterCallGraphOn.Namespace:
                foreach (var fun in all_functions)
                {
                    if (false == functions.TryGetValue(fun.Function.Id, out var fun_node))
                    {
                        continue;
                    }

                    // find namespace of function
                    //fun.Function.
                    // if there is a class, use the namespace of the class instead
                    //fun.Klass

                    // set the cluster of the function to the namespace cluster
                }
                break;
        }

        AnsiConsole.WriteLine("Adding links...");
        foreach (var fun in all_functions)
        {
            if (false == functions.TryGetValue(fun.Function.Id, out var src))
            {
                continue;
            }

            foreach (var r in fun.Function.References)
            {
                if (false == functions.TryGetValue(r.RefId, out var dst))
                {
                    continue;
                }
                g.AddEdge(src, dst);
            }
        }

        AnsiConsole.WriteLine("Writing graph...");
        await g.SmartWriteFileAsync(vfs, paths, cwd, output_file, log);

        static Shape func_to_shape(Method func) => func.Function.Protection switch
        {
            DoxProtectionKind.Public => Shape.Ellipse,
            DoxProtectionKind.Protected => Shape.Diamond,
            DoxProtectionKind.Private => Shape.Box,
            DoxProtectionKind.Package => Shape.Egg, // // internal, kinda like public so should roughly match
            _ => throw new NotImplementedException(),
        };
    }
}
