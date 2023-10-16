using System.Xml;

namespace Workbench.Utils;

internal static class XmlExtensions
{
    public static bool HasAttribute(this XmlNode element, string search)
        => element.Attributes?[search] != null;

    public static string? GetAttributeStringOrNull(this XmlNode element, string name)
        => element.Attributes?[name]?.Value;

    public static int? GetAttributeIntOrNull(this XmlNode element, string name)
    {
        var r = element.GetAttributeStringOrNull(name);
        if (r == null) return null;

        return int.Parse(r);
    }

    public static int GetAttributeInt(this XmlNode element, string name)
        => element.GetAttributeIntOrNull(name)
           ?? throw new Exception("missing int");

    public static string GetAttributeString(this XmlNode element, string name)
        => element.GetAttributeStringOrNull(name)
           ?? throw new Exception($"{element.Name} is missing text attribute \"{name}\"");

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
        return el == null
            ? null
            : converter(el);
    }

    public static T GetFirstElementType<T>(this XmlNode root, string name, Func<XmlElement, T> converter)
        where T : class
        => root.GetFirstElementTypeOrNull(name, converter)
           ?? throw new Exception("Missing required element");

    private static string? GetFirstElementStringOrNull(this XmlNode root, string name)
        => root.GetFirstElementTypeOrNull(name, GetSmartText);

    private static string GetFirstElementString(this XmlNode root, string name)
        => root.GetFirstElementStringOrNull(name)
           ?? throw new Exception("string was null");

    public static string NameOf(this XmlElement element)
    {
        var id = element.GetAttributeStringOrNull("id");
        return id == null
            ? element.Name
            : $"{element.Name}[{id}]";
    }

    public static string PathOf(this XmlElement root_element)
    {
        var result = "";

        var iterator = root_element;
        while (iterator != null)
        {
            result = iterator.NameOf() + "/" + result;
            iterator = iterator.ParentNode as XmlElement;
        }

        return result;
    }

    public static IEnumerable<XmlElement> ElementsNamed(this XmlNode root, string child_name)
        => root.Elements()
            .Where(el => el.Name == child_name);

    public static IEnumerable<XmlElement> ElementsNamed(this IEnumerable<XmlNode> nodes, string child_name)
        => nodes.SelectMany(x => x.ElementsNamed(child_name));

    public static string GetFirstText(this XmlNode node)
        => node.GetFirstTextOrNull()
           ?? throw new Exception("node is missing any text nodes");

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
        => GetSmartTextOrNull(el)
           ?? throw new Exception("Failed to get smart text of node");

    private static string? GetSmartTextOrNull(XmlNode node) =>
        node switch
        {
            XmlText text => text.Value,
            XmlCDataSection cdata => cdata.Value,
            _ => null
        };

    public static string? GetTextOfSubElementOrNull(this XmlNode node, string name)
    {
        var element = node.GetFirstElementOrNull(name);
        if (element == null) return null;

        if (element.ChildNodes.Count > 1) throw new Exception("too many text elements");

        var first_child = element.FirstChild;
        if (first_child == null) return null;

        return GetSmartTextOrNull(first_child);
    }

    public static string GetTextOfSubElement(this XmlNode node, string name)
        => node.GetTextOfSubElementOrNull(name)
           ?? throw new Exception("Failed to get smart text of node");

    public static TEnum? GetAttributeEnumOrNull<TEnum>(this XmlNode root, string name) where TEnum : struct
    {
        var v = root.GetAttributeStringOrNull(name);
        if (v == null) { return null; }

        var (ret, err) = ReflectedValues<TEnum>.Converter.StringToEnum(v);
        if (ret == null) { throw new Exception(err); }

        return ret;
    }

    public static TEnum GetAttributeEnum<TEnum>(this XmlNode root, string name) where TEnum : struct
        => root.GetAttributeEnumOrNull<TEnum>(name)
           ?? throw new Exception("missing required enum");

    public static IEnumerable<T> MapChildren<T>(
        this XmlNode root, Func<string, T> from_string, Func<XmlElement, T> from_element)
    {
        foreach (var node in root.ChildNodes)
        {
            switch (node)
            {
                case XmlElement el:
                    yield return from_element(el);
                    break;
                case XmlText text:
                    yield return from_string(text.Value!);
                    break;
            }
        }
    }
}
