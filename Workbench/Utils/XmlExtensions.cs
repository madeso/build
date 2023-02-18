using Microsoft.Win32.SafeHandles;
using System.Xml;

namespace Workbench.Utils;

internal static class XmlExtensions
{
    public static bool HasAttribute(this XmlNode element, string search)
    {
        var attr = element.Attributes;
        if (attr == null) { return false; }

        XmlAttribute? attribute = attr[search];
        return attribute != null;
    }

    public static string? GetAttributeStringOrNull(this XmlNode element, string name)
    {
        var attr = element.Attributes;
        if (attr == null) { return null; }

        XmlAttribute? attribute = attr[name];
        if (attribute == null) return null;

        else return attribute.Value;
    }

    public static int? GetAttributeIntOrNull(this XmlNode element, string name)
    {
        var r = element.GetAttributeStringOrNull(name);
        if (r == null) return null;

        return int.Parse(r);
    }

    public static int GetAttributeInt(this XmlNode element, string name)
    {
        return element.GetAttributeIntOrNull(name) ?? throw new Exception("missing int");
    }

    public static string GetAttributeString(this XmlNode element, string name)
    {
        string? v = element.GetAttributeStringOrNull(name);
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

    public static XmlElement? GetFirstElementOrNull(this XmlNode root, string name)
    {
        // todo(Gustav): throw if too many
        var arr = root.ElementsNamed(name).ToArray();

        return arr.Length switch
        {
            0 => null,
            1 => arr[0],
            _ => throw new Exception("Too many nodes"),
        };
    }

    public static T? GetFirstElementTypeOrNull<T>(this XmlNode root, string name, Func<XmlElement, T> converter)
        where T : class
    {
        var el = root.GetFirstElementOrNull(name);
        if (el == null) return null;

        return converter(el);
    }

    public static T GetFirstElementType<T>(this XmlNode root, string name, Func<XmlElement, T> converter)
        where T : class
    {
        var r = root.GetFirstElementTypeOrNull(name, converter);
        if (r == null) { throw new Exception("Missing required element"); }

        return r;
    }

    private static string? GetFirstElementStringOrNull(this XmlNode root, string name)
    {
        return root.GetFirstElementTypeOrNull(name, GetSmartText);
    }

    private static string GetFirstElementString(this XmlNode root, string name)
    {
        var s = root.GetFirstElementStringOrNull(name);
        if (s == null) throw new Exception("string was null");

        return s;
    }

    public static string NameOf(this XmlElement element)
    {
        string attribute = "";
        if (HasAttribute(element, "id"))
        {
            attribute = "[" + element.GetAttributeString("id") + "]";
        }
        return element.Name + attribute; ;
    }

    public static string PathOf(this XmlElement element)
    {
        XmlElement c = element;
        string result = "";
        while (c != null)
        {
            result = c.NameOf() + "/" + result;
        }
        return result;
    }

    public static IEnumerable<XmlElement> ElementsNamed(this XmlNode root, string childName)
    {
        foreach (var el in root.Elements())
        {
            if (el.Name != childName) continue;
            yield return el;
        }
    }

    public static IEnumerable<XmlElement> ElementsNamed(this IEnumerable<XmlNode> nodes, string childName)
    {
        return nodes.SelectMany(x => x.ElementsNamed(childName));
    }

    public static string GetFirstText(this XmlNode node)
    {
        var result = node.GetFirstTextOrNull();
        if (result == null) throw new Exception("node is missing any text nodes");

        return result;
    }

    public static string? GetFirstTextOrNull(this XmlNode node)
    {
        if (node.ChildNodes.Count > 1)
        {
            throw new Exception("Too many child nodes");
        }
        foreach (XmlNode n in node.ChildNodes)
        {
            var s = GetSmartTextOrNull(n);
            if (s != null) return s;
        }
        return null;
    }

    public static string GetSmartText(this XmlNode el)
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
        var el = node.GetFirstElementOrNull(p);
        if (el == null) { return null; }

        if (el.ChildNodes.Count > 1)
        {
            throw new Exception("too many text elements");
        }

        var ch = el.FirstChild;
        if (ch == null) { return null; }

        return GetSmartTextOrNull(ch);
    }

    public static string GetTextOfSubElement(this XmlNode node, string p)
    {
        var t = node.GetTextOfSubElementOrNull(p);
        if (t == null) throw new Exception("Failed to get smart text of node");

        return t;
    }

    public static E? GetAttributeEnumOrNull<E>(this XmlNode root, string name) where E : struct
    {
        var v = root.GetAttributeStringOrNull(name);
        if (v == null) { return null; }

        var (ret, err) = ReflectedValues<E>.Converter.StringToEnum(v);
        if (ret == null) { throw new Exception(err); }

        return ret;
    }

    public static E GetAttributeEnum<E>(this XmlNode root, string name) where E : struct
    {
        return root.GetAttributeEnumOrNull<E>(name) ?? throw new Exception("missing required enum");
    }

    public static IEnumerable<T> MapChildren<T>(this XmlNode root, Func<string, T> fromstr, Func<XmlElement, T> fromel)
    {
        foreach (var node in root.ChildNodes)
        {
            if (node is XmlElement el)
            {
                yield return fromel(el);
            }
            else if (node is XmlText text)
            {
                yield return fromstr(text.Value!);
            }
        }
    }
}
