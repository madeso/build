using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Workbench.Doxygen.Compound;

namespace Workbench;

public class Dependencies
{
    internal static void WriteToGraphviz(Printer printer, string doxygenXml, string namespaceName, string outputFile, ImmutableHashSet<string> ignoredClasses, bool includeFunctions, bool addArguments)
    {
        const string NO_NAMESPACE = "|";

        printer.Info($"include funcs {includeFunctions}, add args {addArguments}");

        printer.Info("Parsing doxygen XML...");
        var dox = Doxygen.Doxygen.ParseIndex(doxygenXml);
        var rootNamespace = namespaceName == NO_NAMESPACE ? null : DoxygenUtils.FindNamespace(dox, namespaceName);

        if (namespaceName != NO_NAMESPACE && rootNamespace == null)
        {
            printer.Error($"Unknown namespace {namespaceName}");
            return;
        }

        printer.Info("Working...");
        var namespaces = (
                rootNamespace == null
                ? DoxygenUtils.AllNamespaces(dox)
                : DoxygenUtils.IterateNamespaces(dox, rootNamespace)
            ).ToImmutableArray();

        var g = new Graphviz();
        var classes = new Dictionary<string, Graphviz.Node>();

        printer.Info("Adding classes...");
        foreach (var k in namespaces.SelectMany(ns => DoxygenUtils.IterateClassesInNamespace(dox, ns)))
        {
            if(ignoredClasses.Contains(k.name))
            {
                continue;
            }

            var node = g.AddNode(k.name, Shape.box3d);
            classes!.Add(k.refid, node);
        }

        printer.Info("Adding typedefs...");
        foreach (var k in namespaces.SelectMany(ns => DoxygenUtils.AllMembersInNamespace(ns, DoxSectionKind.Typedef)))
        {
            if (ignoredClasses.Contains(k.Name))
            {
                continue;
            }

            var node = g.AddNode(k.Name, Shape.box);
            classes!.Add(k.Id, node);

            var existingRefs = new HashSet<string>();
            AddTypeLink(g, classes, () => node, existingRefs, k.Type);
        }

        printer.Info("Adding members for class...");
        foreach (var klass in namespaces.SelectMany(ns => DoxygenUtils.IterateClassesInNamespace(dox, ns)))
        {
            if(false == classes.TryGetValue(klass.refid, out var graphvizKlass))
            {
                continue;
            }

            var existingRefs = new HashSet<string>();

            // add inheritence
            foreach(var r in klass.Compund.Compound.Basecompoundref)
            {
                if(r.refid == null) continue;
                AddReference(r.refid, g, classes, () => graphvizKlass, existingRefs);
            }

            
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
            printer.Info("Adding functions...");
            foreach (var func in namespaces.SelectMany(ns => DoxygenUtils.AllMembersInNamespace(ns, DoxSectionKind.Func)))
            {
                var existingRefs = new HashSet<string>();

                Graphviz.Node? funcNode = null;
                Graphviz.Node getFuncNode()
                {
                    if(funcNode == null)
                    {
                        funcNode = g.AddNode($"{func.Name}{func.Argsstring}", Shape.ellipse);
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
        printer.Info("Parsing doxygen XML...");
        var dox = Doxygen.Doxygen.ParseIndex(doxygenXml);
        var rootNamespace = DoxygenUtils.FindNamespace(dox, namespaceName);

        if (rootNamespace == null)
        {
            printer.Error($"Unknown namespace {namespaceName}");
            return;
        }

        printer.Info("Working...");
        var namespaces = DoxygenUtils.IterateNamespaces(dox, rootNamespace).ToImmutableArray();

        foreach (var k in namespaces.SelectMany(ns => DoxygenUtils.IterateClassesInNamespace(dox, ns)))
        {
            AnsiConsole.MarkupLineInterpolated($"Class {k.name}");
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
}
