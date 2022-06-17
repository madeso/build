﻿use std::collections::HashMap;
use std::path::Path;
use crate::core;

pub struct Graphviz
{
    nodes: Vec<Node>,
    id_to_node: HashMap<String, usize>,
    edges: Vec<Edge>
}


pub struct Node
{
    id: String,
    display: String,
    shape: String
}

pub struct Edge
{
    from: usize,
    to: usize
}

impl Graphviz
{
    /*
    fn AddNode(display: String, shape: String) -> &Node
    {
        var id = ReplaceNonId(display.ToLower().Trim().Replace(" ", ""));
        var baseId = id;
        var index = 2;
        while(Nodes.ContainsKey(id) == true)
        {
            id = $"{baseId}_{index}";
            index += 1;
        }
    }
    */

    pub fn new() -> Graphviz
    {
        Graphviz
        {
            nodes: Vec::new(),
            id_to_node: HashMap::new(),
            edges: Vec::new()
        }
    }

    pub fn add_node_with_id(&mut self, display: String, shape: String, id: String) -> &mut Node
    {
        let index = self.nodes.len();
        self.id_to_node.insert(id.clone(), index);
        self.nodes.push(Node{id, display, shape});
        &mut self.nodes[index]
    }

    pub fn get_node_id(&self, id: String) -> Option<usize>
    {
        match self.id_to_node.get(&id)
        {
            Some(x) => Some(*x),
            None => None
        }
    }

    pub fn add_edge(&mut self, from: usize, to: usize)
    {
        self.edges.push(Edge{from, to});
    }

    pub fn write_file_to(&self, path: &Path)
    {
        let mut source = String::new();

        source.push_str("digraph G\n");
        source.push_str("{\n");
        for n in &self.nodes
        {
            source.push_str(format!("    {} [label=\"{}\" shape={}];\n", n.id, n.display, n.shape).as_str());
        }
        source.push_str("\n");
        for e in &self.edges
        {
            let from = &self.nodes[e.from];
            let to = &self.nodes[e.to];
            source.push_str(format!("    {} -> {};\n", from.id, to.id).as_str());
        }
        source.push_str("}\n");

        core::write_string_to_file_or(&path, &source).unwrap();
    }
}

/*
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
*/

