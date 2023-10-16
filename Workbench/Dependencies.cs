using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;
using Workbench.Doxygen.Compound;
using Workbench.Doxygen.Index;
using Workbench.Utils;

namespace Workbench;

public class Dependencies
{
    const string NO_NAMESPACE = "|";

    internal static void WriteToGraphviz(Printer printer, string doxygenXml, string namespaceName, string outputFile, ImmutableHashSet<string> ignoredClasses, bool includeFunctions, bool addArguments, bool addMembers, bool clusterNamespce)
    {
        Printer.Info("Parsing doxygen XML...");
        var dox = Doxygen.Doxygen.ParseIndex(doxygenXml);
        var rootNamespace = namespaceName == NO_NAMESPACE ? null : DoxygenUtils.FindNamespace(dox, namespaceName);

        if (namespaceName != NO_NAMESPACE && rootNamespace == null)
        {
            printer.Error($"Unknown namespace {namespaceName}");
            return;
        }

        Printer.Info("Working...");
        var namespaces = (
                rootNamespace == null
                ? DoxygenUtils.AllNamespaces(dox)
                : DoxygenUtils.IterateAllNamespaces(dox, rootNamespace)
            ).ToImmutableArray();

        var g = new Graphviz();
        var classes = new Dictionary<string, Graphviz.Node>();

        Printer.Info("Adding classes...");
        foreach (var ns in namespaces)
        {
            foreach(var k in DoxygenUtils.IterateClassesInNamespace(dox, ns))
            {
                if(ignoredClasses.Contains(k.CompoundName))
                {
                    continue;
                }

                var name = clusterNamespce
                    ? k.CompoundName.Split(":", StringSplitOptions.RemoveEmptyEntries).Last()
                    : k.CompoundName;

                var node = g.AddNodeWithId(name, Shape.Box3d, k.Id);
                classes!.Add(k.Id, node);

                node.Cluster = g.FindOrCreateCluster(ns.CompoundName);
            }
        }

        Printer.Info("Adding typedefs...");
        foreach (var k in namespaces.SelectMany(ns => DoxygenUtils.AllMembersInNamespace(ns, DoxSectionKind.Typedef)))
        {
            if (ignoredClasses.Contains(k.Name))
            {
                continue;
            }

            var node = g.AddNode(k.Name, Shape.Box);
            classes!.Add(k.Id, node);

            var existingRefs = new HashSet<string>();
            AddTypeLink(g, classes, () => node, existingRefs, k.Type);
        }

        Printer.Info("Adding members for class...");
        foreach (var klass in namespaces.SelectMany(ns => DoxygenUtils.IterateClassesInNamespace(dox, ns)))
        {
            if(false == classes.TryGetValue(klass.Id, out var graphvizKlass))
            {
                continue;
            }

            var existingRefs = new HashSet<string>();

            // add inheritence
            foreach(var r in klass.BaseCompoundRefs)
            {
                if(r.refid == null) continue;
                AddReference(r.refid, g, classes, () => graphvizKlass, existingRefs);
            }

            if(!addMembers) { continue; }

            foreach (var member in DoxygenUtils.AllMembersForAClass(klass))
            {
                if(includeFunctions == false && member.Kind == DoxMemberKind.Function)
                {
                    continue;
                }

                if (addArguments)
                {
                    foreach (var p in member.Param)
                    {
                        AddTypeLink(g, classes, () => graphvizKlass, existingRefs, p.type);
                    }
                }

                // return value for a function or the type of the member
                AddTypeLink(g, classes, () => graphvizKlass, existingRefs, member?.Type);
            }
        }

        if(includeFunctions)
        {
            Printer.Info("Adding functions...");
            foreach (var func in namespaces.SelectMany(ns => DoxygenUtils.AllMembersInNamespace(ns, DoxSectionKind.Func)))
            {
                var existingRefs = new HashSet<string>();

                Graphviz.Node? funcNode = null;
                Graphviz.Node getFuncNode()
                {
                    if(funcNode == null)
                    {
                        funcNode = g.AddNode($"{func.Name}{func.Argsstring}", Shape.Ellipse);
                    }
                    return funcNode;
                }

                if(addArguments) foreach (var p in func.Param)
                {
                    AddTypeLink(g, classes, getFuncNode, existingRefs, p.type);
                }

                AddTypeLink(g, classes, getFuncNode, existingRefs, func.Type);
            }
        }

        g.SmartWriteFile(outputFile);
    }

    internal static void PrintLists(Printer printer, string doxygenXml, string namespaceName)
    {
        Printer.Info("Parsing doxygen XML...");
        var dox = Doxygen.Doxygen.ParseIndex(doxygenXml);
        var rootNamespace = DoxygenUtils.FindNamespace(dox, namespaceName);

        if (rootNamespace == null)
        {
            printer.Error($"Unknown namespace {namespaceName}");
            return;
        }

        Printer.Info("Working...");
        var namespaces = DoxygenUtils.IterateAllNamespaces(dox, rootNamespace).ToImmutableArray();

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
            AnsiConsole.MarkupLineInterpolated($"Func {func.Name}{func.Argsstring}");
        }
    }

    private static void AddTypeLink(Graphviz g, Dictionary<string, Graphviz.Node> validTypes,
        Func<Graphviz.Node> parentFunc, HashSet<string> existingRefs, linkedTextType? type)
    {
        if(type == null) { return; }

        Graphviz.Node? parent = null;
        foreach (var node in type.Nodes)
        {
            var re = node as linkedTextType.Ref;
            if (re == null) continue;

            AddReference(re.Value.refid, g, validTypes, () =>
                {
                    if(parent != null) return parent;
                    parent = parentFunc();
                    return parent;
                }, existingRefs);
        }
    }

    private static void AddReference(string id, Graphviz g, Dictionary<string, Graphviz.Node> validTypes,
        Func<Graphviz.Node> parentFunc, HashSet<string> existingRefs)
    {
        if (existingRefs.Contains(id)) return;
        existingRefs.Add(id);

        if (false == validTypes.TryGetValue(id, out var linkedKlass)) return;

        g.AddEdge(parentFunc(), linkedKlass);
    }

    [TypeConverter(typeof(EnumTypeConverter<ClusterCallgraphOn>))]
    [JsonConverter(typeof(EnumJsonConverter<ClusterCallgraphOn>))]
    public enum ClusterCallgraphOn
    {
        [EnumString("none")]
        None,

        [EnumString("class")]
        Class,

        [EnumString("namespace")]
        Namespace
    }

    private record Method(CompoundDef? Klass, memberdefType Function);
    internal static void WriteCallgraphToGraphviz(Printer printer, string doxygenXml, string namespaceFilter, string outputFile, ClusterCallgraphOn clusterOn)
    {
        // todo(Gustav): option to remove namespace prefixes

        Printer.Info("Parsing doxygen XML...");
        var dox = Doxygen.Doxygen.ParseIndex(doxygenXml);
        
        Printer.Info("Collecting functions...");
        var namespaces = DoxygenUtils.AllNamespaces(dox).ToImmutableArray();

        
        var memberFunctions = namespaces
            .SelectMany(ns => DoxygenUtils.IterateClassesInNamespace(dox, ns))
            .SelectMany(klass => DoxygenUtils.AllMembersForAClass(klass)
                .Select(fun => new Method(klass, fun))
            )
            // add properties too
            .Where(mem => mem.Function.Kind == DoxMemberKind.Function
                        || mem.Function.Kind == DoxMemberKind.Property)
            ;
        var freeFunctions =
            namespaces.SelectMany(ns => DoxygenUtils.AllMembersInNamespace(ns, DoxSectionKind.Func))
            .Where(fun => fun.Kind == DoxMemberKind.Function
                        || fun.Kind == DoxMemberKind.Property)
            .Select(fun => new Method(null, fun));

        var allFunctions = memberFunctions.Concat(freeFunctions).ToImmutableArray();


        Printer.Info("Adding functions...");
        var g = new Graphviz();
        var functions = new Dictionary<string, Graphviz.Node>();
        foreach (var func in allFunctions)
        {
            var baseName = $"{func.Function.Name}{func.Function.Argsstring}";
            if(clusterOn != ClusterCallgraphOn.Class && func.Klass != null)
            {
                baseName = $"{func.Klass.CompoundName}.{baseName}";
            }
            functions.Add(func.Function.Id, g.AddNodeWithId(baseName, FuncToShape(func), func.Function.Id));
        }

        switch(clusterOn)
        {
            case ClusterCallgraphOn.Class:
                foreach(var klass in namespaces.SelectMany(ns => DoxygenUtils.IterateClassesInNamespace(dox, ns)))
                {
                    var cluster = g.FindOrCreateCluster(klass.Id, klass.CompoundName);
                    foreach(var fun in DoxygenUtils.AllMembersForAClass(klass)
                        .Where(mem => mem.Kind == DoxMemberKind.Function))
                    {
                        if (false == functions.TryGetValue(fun.Id, out var funNode))
                        {
                            continue;
                        }

                        funNode.Cluster = cluster;
                    }
                }
                break;
            case ClusterCallgraphOn.Namespace:
                foreach(var fun in allFunctions)
                {
                    if (false == functions.TryGetValue(fun.Function.Id, out var funNode))
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
        
        Printer.Info("Adding links...");
        foreach (var fun in allFunctions)
        {
            if(false == functions.TryGetValue(fun.Function.Id, out var src))
            {
                continue;
            }

            foreach(var r in fun.Function.References)
            {
                if (false == functions.TryGetValue(r.refid, out var dst))
                {
                    continue;
                }
                g.AddEdge(src, dst);
            }
        }

        Printer.Info("Wrtitng graph...");
        g.SmartWriteFile(outputFile);

        static Shape FuncToShape(Method func) => func.Function.Prot switch
        {
            DoxProtectionKind.Public => Shape.Ellipse,
            DoxProtectionKind.Protected => Shape.Diamond,
            DoxProtectionKind.Private => Shape.Box,
            DoxProtectionKind.Package => Shape.Egg, // // internal, kinda like public so should roughly match
            _ => throw new NotImplementedException(),
        };
    }
}
