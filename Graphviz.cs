namespace Workbench;


public class Graphviz
{
    public class Node
    {
        public readonly string id;
        public readonly string display;
        public readonly string shape;
        public readonly string? cluster;

        public Node(string id, string display, string shape, string? cluster)
        {
            this.id = id;
            this.display = display;
            this.shape = shape;
            this.cluster = cluster;
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
    }

    readonly List<Node> nodes = new();
    readonly Dictionary<string, int> id_to_node = new();
    readonly List<Edge> edges = new();

    public Node add_node_with_id(string display, string shape, string id)
    {
        var index = this.nodes.Count;
        this.id_to_node.Add(id, index);
        this.nodes.Add(new Node(id, display, shape, cluster: null));
        return this.nodes[index];
    }

    public string GetUniqueId(string display)
    {
        var id = ReplaceNonId(display.ToLower().Trim().Replace(" ", ""));
        var baseId = id;
        var index = 2;
        while (this.id_to_node.ContainsKey(id) == true)
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

    private static string ReplaceNonId(string v)
    {
        string r = string.Empty;
        bool first = true;
        foreach (var c in v)
        {
            if (first && char.IsLetter(c) || c == '_')
            {
                r += c;
            }
            else if (char.IsLetter(c) || char.IsNumber(c) || c == '_')
            {
                r += c;
            }
        }

        if (r.Length == 0)
        {
            return "node";
        }

        return r;
    }

    private int? get_node_id(string id)
    {
        if(this.id_to_node.TryGetValue(id, out var ret))
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
        this.edges.Add(new Edge(from, to));
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

            foreach (var group in this.nodes.GroupBy(x => x.cluster))
            {
                var nodes = group.ToList();
                var cluster = nodes.FirstOrDefault()?.cluster;
                var indent = string.Empty;
                if (cluster != null)
                {
                    yield return $"    subgraph cluster_{cluster} {{\n";
                    indent = "    ";
                }

                foreach (var n in nodes)
                {
                    yield return $"    {indent}{n.id} [label=\"{n.display}\" shape={n.shape}];\n";
                }

                if (cluster != null)
                {
                    yield return "    }\n";
                }
            }

            yield return "\n";
            foreach (var e in this.edges)
            {
                var from = e.from;
                var to = e.to;
                yield return $"    {from.id} -> {to.id};\n";
            }

            yield return "}\n";
        }
    }

    private IEnumerable<Node> get_all_dependencies_for_node(Node thisnode)
    {
        foreach(var e in this.edges)
        {
            if(e.from == thisnode)
            {
                yield return e.to;
            }
        }
    }

    private void deep_add_all_dependencies(HashSet<Node> children, Node node, bool add)
    {
        var deps = this.get_all_dependencies_for_node(node);
        foreach(var p in deps)
        {
            if(p == null) { throw new Exception("invalid internal state"); }

            if(add)
            {
                children.Add(p);
            }

            this.deep_add_all_dependencies(children, p, true);
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
        if(this.nodes.Count <= 0)
        {
            return;
        }
        
        foreach(var node in this.nodes)
        {
            // get all unique dependencies
            var se = new HashSet<Node>();
            this.deep_add_all_dependencies(se, node, false);

            // get all dependencies from current, and remove all from list
            var deps = this.get_all_dependencies_for_node(node);
            this.edges.RemoveAll(e => e.from == node);

            // add them back
            foreach(var dependency in deps)
            {
                if(se.Contains(dependency) == false)
                {
                    this.AddEdge(node, dependency);
                }
            }
        }
    }
}
