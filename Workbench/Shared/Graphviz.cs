using System.Collections.Immutable;
using Workbench.Shared.Extensions;

namespace Workbench.Shared;

public enum Shape
{
    Box,
    Polygon,
    Ellipse,
    Oval,
    Circle,
    Point,
    Egg,
    Triangle,
    PlainText,
    Plain,
    Diamond,
    Trapezium,
    Parallelogram,
    House,
    Pentagon,
    Hexagon,
    Septagon,
    Octagon,
    DoubleCircle,
    DoubleOctagon,
    TripleOctagon,
    InvertedTriangle,
    InvertedTrapezium,
    InvertedHouse,
    Mdiamond,
    SquareWithDiagonals,
    CircleWithDiagonals,
    Rect,
    Rectangle,
    Square,
    Star,
    None,
    Underline,
    Cylinder,
    Note,
    Tab,
    Folder,
    Box3d,
    Component,

    // excluded shapes for synthetic biology since they are not at useful for us
}

public class Graphviz
{
    public class Cluster
    {
        public Cluster(string id, string label)
        {
            Id = id;
            Label = label;
        }

        public string Id { get; }
        public string Label { get; set; }
    }

    public class Node
    {
        public readonly string Id;
        public string Display;
        public readonly Shape Shape;
        public Cluster? Cluster;

        public Node(string id, string display, Shape shape, Cluster? cluster)
        {
            Id = id;
            Display = display;
            Shape = shape;
            Cluster = cluster;
        }

        public override string ToString()
        {
            return Display;
        }
    }

    public class Edge
    {
        public readonly Node From;
        public readonly Node To;

        public Edge(Node from, Node to)
        {
            From = from;
            To = to;
        }

        public override string ToString()
        {
            return $"{From} -> {To}";
        }
    }

    private readonly List<Node> nodes = new();
    private readonly Dictionary<string, Node> id_to_node = new();
    private readonly List<Edge> edges = new();

    public int NodeCount => nodes.Count;
    public int EdgeCount => edges.Count;

    private readonly Dictionary<string, Cluster> clusters = new();

    public Cluster FindOrCreateCluster(string id, string display)
    {
        var c = clusters.GetValueOrDefault(id);
        if (c != null)
        {
            return c;
        }
        c = new Cluster(id, display);
        clusters.Add(id, c);
        return c;
    }

    public Cluster FindOrCreateCluster(string display)
    {
        return FindOrCreateCluster(ConvertIntoSafeId(display, "cluster"), display);
    }

    public Node AddNodeWithId(string display, Shape shape, string id)
    {
        var new_node = new Node(id, display, shape, cluster: null);
        id_to_node.Add(id, new_node);
        nodes.Add(new_node);
        return new_node;
    }

    public string GetUniqueId(string display)
    {
        var id = ConvertIntoSafeId(display, "node");
        var base_id = id;
        var index = 2;
        while (id_to_node.ContainsKey(id))
        {
            id = $"{base_id}_{index}";
            index += 1;
        }
        return id;
    }

    public Node AddNode(string display, Shape shape)
    {
        var id = GetUniqueId(display);
        return AddNodeWithId(display, shape, id);
    }

    private static string ConvertIntoSafeId(string a, string default_name)
    {
        var suggested_id = a.ToLower().Trim().Replace(" ", "").Replace("::", "_");

        var cleaned_id = string.Empty;
        var first = true;
        foreach (var c in suggested_id)
        {
            if (first && char.IsLetter(c) || c == '_')
            {
                cleaned_id += c;
            }
            else if (char.IsLetter(c) || char.IsNumber(c) || c == '_')
            {
                cleaned_id += c;
            }

            first = false;
        }

        if (cleaned_id.Length == 0)
        {
            return default_name;
        }

        if (cleaned_id == "node") { return "_" + cleaned_id; }

        return cleaned_id;
    }

    public Node? GetNodeFromId(string id)
    {
        return id_to_node.TryGetValue(id, out var ret)
            ? ret
            : null
            ;
    }

    public Node GetOrCreate(string display, Shape shape = Shape.Box)
    {
        return GetNodeFromId(ConvertIntoSafeId(display, "node"))
               ?? AddNode(display, shape);
    }

    public void AddEdge(Node from, Node to)
    {
        edges.Add(new Edge(from, to));
    }

    public void WriteFile(Fil path)
    {
        path.WriteAllLines(Lines);
    }

    public async Task WriteFileAsync(Fil path)
    {
        await path.WriteAllLinesAsync(Lines);
    }

    public async Task<string[]> WriteSvgAsync(Dir cwd, Log log)
    {
        var dot = Config.Paths.GetGraphvizExecutable(cwd, log);

        if (dot == null)
        {
            return Array.Empty<string>();
        }

        var cmdline = new ProcessBuilder(
            dot,
            "-Tsvg"
        );
        var output = await cmdline.RunAndGetOutputAsync(cwd, Lines);

        if (output.ExitCode != 0)
        {
            log.Error($"Non zero return from calling dot: {output.ExitCode}");
            foreach (var err in output.Output.Where(x => x.IsError))
            {
                Console.WriteLine(err.Line);
            }

            return Array.Empty<string>();
        }

        var ret = output.Output
            .Where(x => x.IsError == false)
            .Select(x => x.Line).ToArray();

        return ret;
    }

    public async IAsyncEnumerable<string> WriteHtmlAsync(Dir cwd, Log log, Fil file, bool use_max_width = false)
    {
        var svg = await WriteSvgAsync(cwd, log);

        yield return "<!DOCTYPE html>";
        yield return "<html>";

        yield return "<head>";

        if (use_max_width)
        {
            yield return "<style>";
            yield return "html, body, .container, svg {";
            yield return "  width: 100%;";
            yield return "  height: 100%;";
            yield return "  margin: 0;";
            yield return "  padding: 0;";
            yield return "}";
            yield return "</style>";
        }
        yield return $"<title>{file.NameWithoutExtension}</title>";
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
        yield return "    // Expose to window namespace for testing purposes";
        yield return "    window.zoomTiger = svgPanZoom('#container svg', {";
        yield return "        zoomEnabled: true";
        yield return "      , controlIconsEnabled: true";
        yield return "      , fit: true";
        yield return "      , zoomScaleSensitivity: 0.2";
        yield return "      , minZoom: 0.001";
        yield return "      , maxZoom: 10000";
        yield return "      , fit: true";
        yield return "      , center: true";
        yield return "    });";
        yield return "};";
        yield return "</script>";

        yield return "</body>";

        yield return "</html>";
    }

    public async Task SmartWriteFileAsync(Dir cwd, Fil path, Log log)
    {
        var am = new ActionMapper();
        am.Add(async () =>
        {
            await WriteFileAsync(path);
        }, "", ".gv", ".graphviz", ".dot");
        am.Add(async () =>
        {
            // todo(Gustav): don't write svg if we failed
            await path.WriteAllLinesAsync(await WriteSvgAsync(cwd, log));
        }, ".svg");
        am.Add(async () =>
        {
            // todo(Gustav): don't write html if we failed
            await path.WriteAllLinesAsync(await WriteHtmlAsync(cwd, log, path).ToListAsync());
        }, ".htm", ".html");

        var ext = path.Extension;
        if (false == await am.Run(ext))
        {
            log.Error($"Unknown extension {ext}, supported: {StringListCombiner.EnglishAnd().Combine(am.Names)}");
        }
    }

    public IEnumerable<string> Lines
    {
        get
        {
            yield return "digraph G";
            yield return "{";

            foreach (var grouped_nodes in nodes.GroupBy(x => x.Cluster).Select(x => x.ToList()))
            {
                var cluster = grouped_nodes.FirstOrDefault()?.Cluster;
                var indent = string.Empty;
                if (cluster != null)
                {
                    yield return $"    subgraph cluster_{cluster.Id} {{";
                    indent = "    ";
                    yield return $"    {indent}label = \"{Escape(cluster.Label)}\";";
                    yield return $"    {indent}color=blue;";
                }

                foreach (var n in grouped_nodes)
                {
                    yield return $"    {indent}{n.Id} [label=\"{Escape(n.Display)}\" shape={ShapeToString(n.Shape)}];";
                }

                if (cluster != null)
                {
                    yield return "    }";
                }
            }

            yield return "";
            foreach (var e in edges)
            {
                var from = e.From;
                var to = e.To;
                yield return $"    {from.Id} -> {to.Id};";
            }

            yield return "}";
        }
    }

    private object ShapeToString(Shape shape)
    {
        return shape switch
        {
            Shape.Box => "box",
            Shape.Polygon => "polygon",
            Shape.Ellipse => "ellipse",
            Shape.Oval => "oval",
            Shape.Circle => "circle",
            Shape.Point => "point",
            Shape.Egg => "egg",
            Shape.Triangle => "triangle",
            Shape.PlainText => "plaintext",
            Shape.Plain => "plain",
            Shape.Diamond => "diamond",
            Shape.Trapezium => "trapezium",
            Shape.Parallelogram => "parallelogram",
            Shape.House => "house",
            Shape.Pentagon => "pentagon",
            Shape.Hexagon => "hexagon",
            Shape.Septagon => "septagon",
            Shape.Octagon => "octagon",
            Shape.DoubleCircle => "doublecircle",
            Shape.DoubleOctagon => "doubleoctagon",
            Shape.TripleOctagon => "tripleoctagon",
            Shape.InvertedTriangle => "invtriangle",
            Shape.InvertedTrapezium => "invtrapezium",
            Shape.InvertedHouse => "invhouse",
            Shape.Mdiamond => "Mdiamond",
            Shape.SquareWithDiagonals => "Msquare",
            Shape.CircleWithDiagonals => "Mcircle",
            Shape.Rect => "rect",
            Shape.Rectangle => "rectangle",
            Shape.Square => "square",
            Shape.Star => "star",
            Shape.None => "none",
            Shape.Underline => "underline",
            Shape.Cylinder => "cylinder",
            Shape.Note => "note",
            Shape.Tab => "tab",
            Shape.Folder => "folder",
            Shape.Box3d => "box3d",
            Shape.Component => "component",
            _ => throw new NotImplementedException(),
        };
    }

    private static string Escape(string display)
    {
        var r = "";
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

    private IEnumerable<Node> GetAllDependenciesForNode(Node this_node)
    {
        return edges
                .Where(e => e.From == this_node)
                .Select(e => e.To)
            ;
    }

    private void DeepAddAllDependencies(HashSet<Node> children, Node node, bool add)
    {
        var deps = GetAllDependenciesForNode(node);
        foreach (var p in deps)
        {
            if (p == null) { throw new Exception("invalid internal state"); }

            if (add)
            {
                children.Add(p);
            }

            DeepAddAllDependencies(children, p, true);
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
            DeepAddAllDependencies(se, node, false);

            // get all dependencies from current, and remove all from list
            var deps = GetAllDependenciesForNode(node).ToImmutableArray();
            edges.RemoveAll(e => e.From == node);

            // add them back
            foreach (var dependency in deps
                         .Where(dependency => se.Contains(dependency) == false))
            {
                AddEdge(node, dependency);
            }
        }
    }
}
