using System.Collections.Immutable;

namespace Workbench;


public class Graphviz
{
    public class Node
    {
        public readonly string id;
        public readonly string display;
        public readonly string shape;
        public string? cluster;

        public Node(string id, string display, string shape, string? cluster)
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

    public Node add_node_with_id(string display, string shape, string id)
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

    public Node AddNode(string display, string shape)
    {
        var id = GetUniqueId(display);
        return add_node_with_id(display, shape, id);
    }

    private static string ConvertIntoSafeId(string a, string defaultName)
    {
        var suggestedId = a.ToLower().Trim().Replace(" ", "");

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

        return cleanedId;
    }

    public Node? get_node_id(string id)
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

    public void write_file_to(string path)
    {
        File.WriteAllLines(path, Lines);
    }

    public IEnumerable<string> Lines
    {
        get
        {
            yield return "digraph G\n";
            yield return "{\n";

            foreach (var group in nodes.GroupBy(x => x.cluster))
            {
                var nodes = group.ToList();
                var cluster = nodes.FirstOrDefault()?.cluster;
                var indent = string.Empty;
                if (cluster != null)
                {
                    yield return $"    subgraph cluster_{ConvertIntoSafeId(cluster, "cluster")} {{\n";
                    indent = "    ";
                }

                foreach (var n in nodes)
                {
                    yield return $"    {indent}{n.id} [label=\"{Escape(n.display)}\" shape={n.shape}];\n";
                }

                if (cluster != null)
                {
                    yield return "    }\n";
                }
            }

            yield return "\n";
            foreach (var e in edges)
            {
                var from = e.from;
                var to = e.to;
                yield return $"    {from.id} -> {to.id};\n";
            }

            yield return "}\n";
        }
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
