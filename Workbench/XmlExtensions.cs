using System.Xml;

namespace Workbench;

internal static class XmlExtensions
{
    public static IEnumerable<XmlElement> FindAll(this XmlNode root, string xpath)
    {
        var nodes = root.SelectNodes(xpath);
        if (nodes != null)
        {
            foreach ( var n in nodes )
            {
                var e = n as XmlElement;
                if(e != null)
                {
                    yield return e;
                }
            }
        }
    }
}
