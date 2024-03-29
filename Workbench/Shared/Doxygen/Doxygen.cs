using System.Xml;
using Workbench.Shared.Extensions;

namespace Workbench.Shared.Doxygen;

internal static class Doxygen
{
    public static DoxygenType ParseIndex(Dir dir)
    {
        var path = dir.GetFile("index.xml");
        XmlDocument doc = new();
        doc.Load(path.Path);
        var root = doc.ElementsNamed("doxygenindex").First();
        var parsed = new DoxygenType(new CompoundLoader(dir), root);
        return parsed;
    }
}