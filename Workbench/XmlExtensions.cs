using System.Xml;

namespace Workbench;

internal static class XmlExtensions
{
    public static bool HasAttribute(this XmlNode element, string search)
    {
        var attr = element.Attributes;
        if(attr == null) { return false; }

        XmlAttribute? attribute = attr[search];
        return attribute != null;
    }

    public static string? GetAttributeStringOrNull(this XmlNode element, string name)
    {
        var attr = element.Attributes;
        if(attr == null) { return null; }

        XmlAttribute? attribute = attr[name];
        if (attribute == null) return null;

        else return attribute.Value;
    }

    public static string GetAttributeString(this XmlNode element, string name)
    {
        string? v = GetAttributeStringOrNull(element, name);
        if (v == null) throw new Exception(element.Name + " is missing text attribute \"" + name + "\"");
        else return v;
    }

    public static IEnumerable<XmlElement> Elements(this XmlNode root)
    {
        foreach (XmlNode node in root.ChildNodes)
        {
            if (node is not XmlElement el) continue;
            yield return el;
        }
    }

    public static string NameOf(this XmlElement element)
    {
        string attribute = "";
        if (HasAttribute(element, "id"))
        {
            attribute = "[" + GetAttributeString(element, "id") + "]";
        }
        return element.Name + attribute; ;
    }

    public static string PathOf(this XmlElement element)
    {
        XmlElement c = element;
        string result = "";
        while (c != null)
        {
            result = NameOf(c) + "/" + result;
        }
        return result;
    }

    public static IEnumerable<XmlElement> ElementsNamed(this XmlNode root, string childName)
    {
        foreach (var el in Elements(root))
        {
            if (el.Name != childName) continue;
            yield return el;
        }
    }

    public static IEnumerable<XmlElement> ElementsNamed(this IEnumerable<XmlNode> nodes, string childName)
    {
        return nodes.SelectMany(x => ElementsNamed(x, childName));
    }

    public static string GetFirstText(this XmlNode node)
    {
        var result = GetFirstTextOrNull(node);
        if (result == null) throw new Exception("node is missing any text nodes");

        return result;
    }

    public static string? GetFirstTextOrNull(this XmlNode node)
    {
        foreach (XmlNode n in node.ChildNodes)
        {
            var s = GetSmartTextOrNull(n);
            if (s != null) return s;
        }
        return null;
    }

    private static string GetSmartText(XmlNode el)
    {
        var s = GetSmartTextOrNull(el);
        if (s == null) throw new Exception("Failed to get smart text of node");

        else return s;
    }
    private static string? GetSmartTextOrNull(XmlNode el)
    {
        return el switch
        {
            XmlText text => text.Value,
            XmlCDataSection cdata => cdata.Value,
            _ => null
        };
    }

    public static string? GetTextOfSubElementOrNull(this XmlNode node, string p)
    {
        var el = node[p];
        if (el == null) { return null; }
        
        var ch = el.FirstChild;
        if(ch == null) { return null; }

        return GetSmartTextOrNull(ch);
    }

    public static string GetTextOfSubElement(this XmlNode node, string p)
    {
        var t = GetTextOfSubElementOrNull(node, p);
        if (t == null) throw new Exception("Failed to get smart text of node");

        return t;
    }
}
