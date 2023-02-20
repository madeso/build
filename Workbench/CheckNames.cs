using Spectre.Console;
using System.Text.RegularExpressions;
using Workbench.Doxygen.Compound;

namespace Workbench;

internal partial class CheckNames
{
    private static readonly Regex CamelCase = GenerateCamelCase();

    // todo(Gustav): read from file?
    private static readonly HashSet<string> ValidNames = new()
    {
        "angle",
        "mat2f",
        "mat3f",
        "mat4f",
        "quatf",
        "rgb",
        "rgba",
        "rgbai",
        "rgbi",
        "size2f",
        "size2i",
        "unit2f",
        "unit3f",
        "vec2f",
        "vec2i",
        "vec3f",
        "vec4f",
    };

    internal static void CheckName(string name, Doxygen.Compound.locationType loc, string root)
    {
        if (name.StartsWith('@')) { return; }
        if(ValidNames.Contains(name)) { return; }

        if (CamelCase.IsMatch(name) == false)
        {
            var file = DoxygenUtils.DoxygenFileToPath(loc, root);
            AnsiConsole.MarkupLineInterpolated($"{file}: ERROR: {name} contains bad name");
        }
    }

    internal static int Run(Printer printer, string doxygenXml, string root)
    {
        var parsed = Doxygen.Doxygen.ParseIndex(doxygenXml);
        foreach(var k in DoxygenUtils.AllClasses(parsed))
        {
            foreach(var mem in k.Compund.Compound.Sectiondef.SelectMany(x => x.memberdef))
            {
                switch(mem.Kind)
                {
                    case Doxygen.Compound.DoxMemberKind.Define:
                        CheckName(mem.Name, mem.Location, root);
                        break;
                    case Doxygen.Compound.DoxMemberKind.Typedef:
                        CheckName(mem.Name, mem.Location, root);
                        break;
                    case Doxygen.Compound.DoxMemberKind.Enum:
                        CheckName(mem.Name, mem.Location, root);
                        break;
                    case Doxygen.Compound.DoxMemberKind.Function:
                        if(mem.Templateparamlist != null)
                        {
                            CheckTemplateArguments(root, mem.Location, mem.Templateparamlist.param);
                        }
                        break;
                }
            }

            // todo(Gustav): check template parameter on current klass

            var name = k.name.Split("::", StringSplitOptions.RemoveEmptyEntries).Last();
            name = name.Split('<', StringSplitOptions.RemoveEmptyEntries)[0];
            CheckName(name, k.Compund.Compound.Location!, root);
        }

        return 0;
    }

    private static void CheckTemplateArguments(string root, locationType location, paramType[] pp)
    {
        foreach (var t in pp)
        {
            foreach (var n in t.type!.Nodes)
            {
                if (n is linkedTextType.Text text)
                {
                    var cmds = text.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (cmds.Length == 2 && cmds[0] == "typename")
                    {
                        // T is valid name for template argument
                        if (cmds[1] == "T") { continue; }
                        CheckName(cmds[1], location, root);
                    }
                }
            }
        }
    }

    [GeneratedRegex("[A-Z][a-z0-9]+([A-Z0-9][a-z0-9]+)*", RegexOptions.Compiled)]
    private static partial Regex GenerateCamelCase();
}
