using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Workbench.Doxygen.Compound.linkedTextType;

namespace Workbench;

public class Dependencies
{
    internal static void Run(Printer printer, string doxygenXml, string namespaceFilter)
    {
        var parsed = Doxygen.Doxygen.ParseIndex(doxygenXml);

        var g = new Graphviz();
        var classes = new Dictionary<string, Graphviz.Node>();

        Graphviz.Node GetClassNode(string id, string name)
        {
            if(classes!.TryGetValue(id, out var ret))
            {
                return ret;
            }
            var node = g!.AddNode(name, "box");
            classes!.Add(id, node);
            return node;
        }

        // loop though all classes first to get a sane name for all classes
        foreach (var k in DoxygenUtils.AllClasses(parsed))
        {
            if(k.name.StartsWith(namespaceFilter) == false) continue;
            GetClassNode(k.refid, k.name);
        }

        foreach (var k in DoxygenUtils.AllClasses(parsed))
        {
            var klass = GetClassNode(k.refid, k.name);
            if (k.name.StartsWith(namespaceFilter) == false) continue;

            var existingRefs = new HashSet<string>();

            foreach (var functionMember in DoxygenUtils.AllMethodsInClass(k))
            {
                // assume all arguments are passed by ref

                // reference return value
                var nodes = functionMember?.Type?.Nodes;
                if(nodes == null) continue;
                foreach (var node in nodes)
                {
                    var re = node as Ref;
                    if(re == null) continue;

                    var id = re.Value.refid;
                    if (existingRefs.Contains(id)) continue;
                    existingRefs.Add(id);

                    var linkedKlass = GetClassNode(re.Value.refid, re.Value.Extension);

                    g.AddEdge(klass, linkedKlass);
                }
            }
        }

        // write graphviz
        foreach(var line in g.Lines)
        {
            printer.Info(line);
        }
    }
}
