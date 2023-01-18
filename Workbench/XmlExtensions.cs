using System.Xml;

namespace Workbench;

internal static class XmlExtensions
{
    public static IEnumerable<XmlElement> ElementsNamed(this XmlNode root, string childName)
    {
        foreach (XmlNode node in root.ChildNodes)
        {
            if (node is not XmlElement el) continue;
            if (el.Name != childName) continue;
            yield return el;
        }
    }

    public static IEnumerable<XmlElement> ElementsNamed(this IEnumerable<XmlNode> nodes, string childName)
    {
        return nodes.SelectMany(x => ElementsNamed(x, childName));
    }
}
