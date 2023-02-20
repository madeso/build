using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Workbench.Doxygen.Compound;
using Workbench.Doxygen.Index;

namespace Workbench;

internal class DoxygenUtils
{
    public static IEnumerable<CompoundType> AllClasses(Doxygen.Index.DoxygenType parsed)
    {
        return parsed.compounds.Where(x => x.kind == Doxygen.Index.CompoundKind.Struct || x.kind == Doxygen.Index.CompoundKind.Class);
    }

    internal static string DoxygenFileToPath(locationType loc, string root)
    {
        var abs = new FileInfo(Path.Join(root, loc.file)).FullName;
        var print = File.Exists(abs) ? abs : loc.file;
        return print;
    }
}
