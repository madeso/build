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
    internal static void Run(Printer printer, string doxygenXml, string namespaceName, string outputFile, ImmutableHashSet<string> ignoredClasses)
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

        var g = new Graphviz();
        var classes = new Dictionary<string, Graphviz.Node>();

        printer.Info("Adding classes...");
        foreach (var k in namespaces.SelectMany(ns => DoxygenUtils.IterateClassesInNamespace(dox, ns)))
        {
            if(ignoredClasses.Contains(k.name))
            {
                continue;
            }

            var node = g.AddNode(k.name, "box");
            classes!.Add(k.refid, node);
        }

        printer.Info("Adding typedefs...");
        foreach (var k in namespaces.SelectMany(ns => DoxygenUtils.AllMembersInNamespace(ns, DoxSectionKind.Typedef)))
        {
            var node = g.AddNode(k.Name, "none");
            classes!.Add(k.Id, node);

            var existingRefs = new HashSet<string>();
            AddTypeLink(g, classes, () => node, existingRefs, k.Type);
        }

        printer.Info("Adding members for class...");
        foreach (var k in namespaces.SelectMany(ns => DoxygenUtils.IterateClassesInNamespace(dox, ns)))
        {
            if(false == classes.TryGetValue(k.refid, out var klass))
            {
                continue;
            }

            var existingRefs = new HashSet<string>();

            // add inheritence
            foreach(var r in k.Compund.Compound.Basecompoundref)
            {
                if(r.refid == null) continue;
                AddReference(r.refid, g, classes, () => klass, existingRefs);
            }

            foreach (var functionMember in DoxygenUtils.AllMethodsInClass(k))
            {
                // assume all arguments are passed by ref
                // and just ignore them

                AddTypeLink(g, classes, () => klass, existingRefs, functionMember?.Type);
            }
        }

        printer.Info("Adding functions...");
        foreach (var f in namespaces.SelectMany(ns => DoxygenUtils.AllMembersInNamespace(ns, DoxSectionKind.Func)))
        {
            var existingRefs = new HashSet<string>();
            AddTypeLink(g, classes, () => g.AddNode($"{f.Name}{f.Argsstring}", "circle"), existingRefs, f.Type);
        }

        g.WriteFile(outputFile);
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
