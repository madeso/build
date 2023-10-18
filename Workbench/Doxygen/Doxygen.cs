using System.Xml;
using Workbench.Utils;

namespace Workbench.Doxygen;

internal static class Doxygen
{
    public static DoxygenType ParseIndex(string dir)
    {
        var path = Path.Join(dir, "index.xml");
        XmlDocument doc = new();
        doc.Load(path);
        var root = doc.ElementsNamed("doxygenindex").First();
        var parsed = new DoxygenType(new CompoundLoader(dir), root);
        return parsed;
    }
}