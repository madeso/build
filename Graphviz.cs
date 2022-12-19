namespace Workbench;

internal class Graphviz
{
    public Dictionary<string, Node> Nodes { get; } = new();
    public List<Edge> Edges { get; } = new();

    public class Node
    {
        public Node(string id, string display, string shape)
        {
            Id = id;
            Display = display;
            Shape = shape;
        }

        public string Id { get; }
        public string Display { get; }
        public string Shape { get; }
    }

    public class Edge
    {
        public Edge(Node from, Node to)
        {
            From = from;
            To = to;
        }

        public Node From { get; }
        public Node To { get; }
    }

    public Node AddNode(string display, string shape)
    {
        var id = ReplaceNonId(display.ToLower().Trim().Replace(" ", ""));
        var baseId = id;
        var index = 2;
        while(Nodes.ContainsKey(id) == true)
        {
            id = $"{baseId}_{index}";
            index += 1;
        }
        var n = new Node(id, display, shape);
        this.Nodes.Add(id, n);
        return n;
    }

    public void AddEdge(Node from, Node to)
    {
        Edges.Add(new Edge(from, to));
    }

    public IEnumerable<string> Lines
    {
        get
        {
            yield return "digraph G";
            yield return "{";
            foreach(var n in Nodes.Values)
            {
                yield return $"    {n.Id} [label=\"{n.Display}\" shape={n.Shape}];";
            }
            yield return "";
            foreach(var e in Edges)
            {
                yield return $"    {e.From.Id} -> {e.To.Id};";
            }
            yield return "}";
        }
    }

    private static string ReplaceNonId(string v)
    {
        string r = string.Empty;
        bool first = true;
        foreach(var c in v)
        {
            if(first && char.IsLetter(c) || c == '_')
            {
                r += c;
            }
            else if(char.IsLetter(c) || char.IsNumber(c) || c == '_')
            {
                r += c;
            }
        }

        if(r.Length == 0)
        {
            return "node";
        }

        return r;
    }
}
