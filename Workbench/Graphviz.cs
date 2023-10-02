using System.Collections.Immutable;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using Workbench.Utils;

namespace Workbench;

public enum Shape
{
    box,
    polygon,
    ellipse,
    oval,
    circle,
    point,
    egg,
    triangle,
    plaintext,
    plain,
    diamond,
    trapezium,
    parallelogram,
    house,
    pentagon,
    hexagon,
    septagon,
    octagon,
    doublecircle,
    doubleoctagon,
    tripleoctagon,
    invtriangle,
    invtrapezium,
    invhouse,
    Mdiamond,
    Msquare,
    Mcircle,
    rect,
    rectangle,
    square,
    star,
    none,
    underline,
    cylinder,
    note,
    tab,
    folder,
    box3d,
    component,
    promoter,
    cds,
    terminator,
    utr,
    primersite,
    restrictionsite,
    fivepoverhang,
    threepoverhang,
    noverhang,
    assembly,
    signature,
    insulator,
    ribosite,
    rnastab,
    proteasesite,
    proteinstab,
    rpromoter,
    rarrow,
    larrow,
    lpromoter
}

public class Graphviz
{
    public class Node
    {
        public readonly string id;
        public string display;
        public readonly Shape shape;
        public string? cluster;

        public Node(string id, string display, Shape shape, string? cluster)
        {
            this.id = id;
            this.display = display;
            this.shape = shape;
            this.cluster = cluster;
        }

        public override string ToString()
        {
            return display;
        }
    }

    public class Edge
    {
        public readonly Node from;
        public readonly Node to;

        public Edge(Node from, Node to)
        {
            this.from = from;
            this.to = to;
        }

        public override string ToString()
        {
            return $"{from} -> {to}";
        }
    }

    private readonly List<Node> nodes = new();
    private readonly Dictionary<string, Node> id_to_node = new();
    private readonly List<Edge> edges = new();

    public Node AddNodeWithId(string display, Shape shape, string id)
    {
        var newNode = new Node(id, display, shape, cluster: null);
        id_to_node.Add(id, newNode);
        nodes.Add(newNode);
        return newNode;
    }

    public string GetUniqueId(string display)
    {
        var id = ConvertIntoSafeId(display, "node");
        var baseId = id;
        var index = 2;
        while (id_to_node.ContainsKey(id) == true)
        {
            id = $"{baseId}_{index}";
            index += 1;
        }
        return id;
    }

    public Node AddNode(string display, Shape shape)
    {
        var id = GetUniqueId(display);
        return AddNodeWithId(display, shape, id);
    }

    private static string ConvertIntoSafeId(string a, string defaultName)
    {
        var suggestedId = a.ToLower().Trim().Replace(" ", "").Replace("::", "_");

        string cleanedId = string.Empty;
        bool first = true;
        foreach (var c in suggestedId)
        {
            if (first && char.IsLetter(c) || c == '_')
            {
                cleanedId += c;
            }
            else if (char.IsLetter(c) || char.IsNumber(c) || c == '_')
            {
                cleanedId += c;
            }
        }

        if (cleanedId.Length == 0)
        {
            return defaultName;
        }

        if(cleanedId == "node") { return "_" + cleanedId; }

        return cleanedId;
    }

    public Node? GetNodeFromId(string id)
    {
        if (id_to_node.TryGetValue(id, out var ret))
        {
            return ret;
        }
        else
        {
            return null;
        }
    }

    public void AddEdge(Node from, Node to)
    {
        edges.Add(new Edge(from, to));
    }

    public void WriteFile(string path)
    {
        File.WriteAllLines(path, Lines);
    }

    public string[] WriteSvg()
    {
        var cmdline = new ProcessBuilder(
            "dot",
            "-Tsvg"
        );
        var output = cmdline.RunAndGetOutput(Lines).RequireSuccess();
        return output;
    }

    public IEnumerable<string> WriteHtml(string file)
    {
        var svg = WriteSvg();

        yield return "<!DOCTYPE html>";
        yield return "<html>";
        yield return "<head>";
        yield return $"<title>{ Path.GetFileNameWithoutExtension(file) }</title>";

        yield return "<script src=\"https://cdn.jsdelivr.net/npm/svg-pan-zoom@3.6.1/dist/svg-pan-zoom.min.js\"></script>";

        yield return "</head>";

        yield return "<body>";


        yield return "<div id=\"container\">";
        foreach (var l in svg)
        {
            yield return $"  {l}";
        }
        yield return "</div>";

        yield return "";
        yield return "";

        yield return "<script>";
        yield return "window.onload = function() {";
        yield return "    // Expose to window namespase for testing purposes";
        yield return "    window.zoomTiger = svgPanZoom('#container svg', {";
        yield return "    zoomEnabled: true,";
        yield return "  controlIconsEnabled: true,";
        yield return "  fit: true,";
        yield return "  center: true,";
        yield return "});";
        yield return "};";
        yield return "</script>";

        yield return "</body>";

        yield return "</html>";
    }

    public void SmartWriteFile(string path)
    {
        switch(Path.GetExtension(path))
        {
            case "":
            case ".gv":
            case ".graphviz":
            case ".dot":
                WriteFile(path);
                break;
            case ".svg":
                File.WriteAllLines(path, WriteSvg());
                break;
            case ".html":
                File.WriteAllLines(path, WriteHtml(path));
                break;
        }
    }

    public IEnumerable<string> Lines
    {
        get
        {
            yield return "digraph G";
            yield return "{";

            foreach (var group in nodes.GroupBy(x => x.cluster))
            {
                var nodes = group.ToList();
                var cluster = nodes.FirstOrDefault()?.cluster;
                var indent = string.Empty;
                if (cluster != null)
                {
                    yield return $"    subgraph cluster_{ConvertIntoSafeId(cluster, "cluster")} {{";
                    indent = "    ";
                }

                foreach (var n in nodes)
                {
                    yield return $"    {indent}{n.id} [label=\"{Escape(n.display)}\" shape={ShapeToString(n.shape)}];";
                }

                if (cluster != null)
                {
                    yield return "    }";
                }
            }

            yield return "";
            foreach (var e in edges)
            {
                var from = e.from;
                var to = e.to;
                yield return $"    {from.id} -> {to.id};";
            }

            yield return "}";
        }
    }

    private object ShapeToString(Shape shape)
    {
        return shape switch
        {
            Shape.box => "box",
            Shape.polygon => "polygon",
            Shape.ellipse => "ellipse",
            Shape.oval => "oval",
            Shape.circle => "circle",
            Shape.point => "point",
            Shape.egg => "egg",
            Shape.triangle => "triangle",
            Shape.plaintext => "plaintext",
            Shape.plain => "plain",
            Shape.diamond => "diamond",
            Shape.trapezium => "trapezium",
            Shape.parallelogram => "parallelogram",
            Shape.house => "house",
            Shape.pentagon => "pentagon",
            Shape.hexagon => "hexagon",
            Shape.septagon => "septagon",
            Shape.octagon => "octagon",
            Shape.doublecircle => "doublecircle",
            Shape.doubleoctagon => "doubleoctagon",
            Shape.tripleoctagon => "tripleoctagon",
            Shape.invtriangle => "invtriangle",
            Shape.invtrapezium => "invtrapezium",
            Shape.invhouse => "invhouse",
            Shape.Mdiamond => "Mdiamond",
            Shape.Msquare => "Msquare",
            Shape.Mcircle => "Mcircle",
            Shape.rect => "rect",
            Shape.rectangle => "rectangle",
            Shape.square => "square",
            Shape.star => "star",
            Shape.none => "none",
            Shape.underline => "underline",
            Shape.cylinder => "cylinder",
            Shape.note => "note",
            Shape.tab => "tab",
            Shape.folder => "folder",
            Shape.box3d => "box3d",
            Shape.component => "component",
            Shape.promoter => "promoter",
            Shape.cds => "cds",
            Shape.terminator => "terminator",
            Shape.utr => "utr",
            Shape.primersite => "primersite",
            Shape.restrictionsite => "restrictionsite",
            Shape.fivepoverhang => "fivepoverhang",
            Shape.threepoverhang => "threepoverhang",
            Shape.noverhang => "noverhang",
            Shape.assembly => "assembly",
            Shape.signature => "signature",
            Shape.insulator => "insulator",
            Shape.ribosite => "ribosite",
            Shape.rnastab => "rnastab",
            Shape.proteasesite => "proteasesite",
            Shape.proteinstab => "proteinstab",
            Shape.rpromoter => "rpromoter",
            Shape.rarrow => "rarrow",
            Shape.larrow => "larrow",
            Shape.lpromoter => "lpromoter",
            _ => throw new NotImplementedException(),
        };
    }

    private string Escape(string display)
    {
        string r = "";
        foreach (var c in display)
        {
            r += c switch
            {
                '\\' => @"\\",
                '"' => "\\\"",
                _ => c
            };
        }
        return r;
    }

    private IEnumerable<Node> get_all_dependencies_for_node(Node thisnode)
    {
        foreach (var e in edges)
        {
            if (e.from == thisnode)
            {
                yield return e.to;
            }
        }
    }

    private void deep_add_all_dependencies(HashSet<Node> children, Node node, bool add)
    {
        var deps = get_all_dependencies_for_node(node);
        foreach (var p in deps)
        {
            if (p == null) { throw new Exception("invalid internal state"); }

            if (add)
            {
                children.Add(p);
            }

            deep_add_all_dependencies(children, p, true);
        }
    }

    /*
    given the dependencies like:
    a -> b
    b -> c
    a -> c
    simplify will remove the last dependency (a->c) to 'simplify' the graph
    */
    public void Simplify()
    {
        if (nodes.Count <= 0)
        {
            return;
        }

        foreach (var node in nodes)
        {
            // get all unique dependencies
            var se = new HashSet<Node>();
            deep_add_all_dependencies(se, node, false);

            // get all dependencies from current, and remove all from list
            var deps = get_all_dependencies_for_node(node).ToImmutableArray();
            edges.RemoveAll(e => e.from == node);

            // add them back
            foreach (var dependency in deps)
            {
                if (se.Contains(dependency) == false)
                {
                    AddEdge(node, dependency);
                }
            }
        }
    }
}
