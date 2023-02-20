using Spectre.Console;
using System.Text.RegularExpressions;
using Workbench.Doxygen.Compound;
using Workbench.Doxygen.Index;

namespace Workbench;

internal class CheckNames
{
    // todo(Gustav): read from file?
    private static readonly HashSet<string> ValidTypesNames = new()
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
    private static readonly HashSet<string> ValidMethodNames = new()
    {
        "operator+",
        "operator-",
        "operator*",
        "operator/",
        "operator<",
        "operator<<",
        "operator++",
        "operator->",
        "operator()",
        "operator[]",
        "operator=",

        "operator+=",
        "operator-=",
        "operator*=",
        "operator/=",
        "operator==",
        "operator!=",

    };
    private static readonly HashSet<string> NoValidNames = new();


    int namesChecked = 0;
    int errorsDetected = 0;
    private Printer printer;

    public CheckNames(Printer printer)
    {
        this.printer = printer;
    }

    internal void CheckName(string name, Doxygen.Compound.locationType loc, string root, Regex CamelCase, HashSet<string> validNames, string source)
    {
        if (name.StartsWith('@')) { return; }

        namesChecked += 1;
        if(validNames.Contains(name)) { return; }

        if (CamelCase.IsMatch(name) == false)
        {
            errorsDetected += 1;
            var file = DoxygenUtils.LocationToString(loc, root);

            printer.Error(file, $"{name} is a invalid name for {source}");
        }
    }

    internal static int Run(Printer printer, string doxygenXml, string root)
    {
        var parsed = Doxygen.Doxygen.ParseIndex(doxygenXml);

        CheckNames runner = new(printer);
        foreach (var k in DoxygenUtils.AllClasses(parsed))
        {
            runner.CheckClass(root, k);
        }

        AnsiConsole.MarkupLineInterpolated($"Detected [red]{runner.errorsDetected}[/] in [blue]{runner.namesChecked}[/] names");

        return runner.errorsDetected > 0 ? -1 : 0;
    }

    private void CheckClass(string root, CompoundType k)
    {
        foreach (var mem in k.Compund.Compound.Sectiondef.SelectMany(x => x.memberdef))
        {
            switch (mem.Kind)
            {
                case Doxygen.Compound.DoxMemberKind.Define:
                    CheckDefine(root, mem);
                    break;
                case Doxygen.Compound.DoxMemberKind.Typedef:
                    CheckTypedef(root, mem);
                    break;
                case Doxygen.Compound.DoxMemberKind.Enum:
                    CheckEnum(root, mem);
                    break;
                case Doxygen.Compound.DoxMemberKind.Function:
                    CheckFunction(root, mem);
                    if(DoxygenUtils.IsConstructorOrDestructor(mem) == false)
                    {
                        CheckName(mem.Name, mem.Location, root, CaseMatch.LowerSnakeCase, ValidMethodNames, "method");
                    }
                    break;
            }
        }

        // todo(Gustav): check template parameter on current klass

        var name = k.name.Split("::", StringSplitOptions.RemoveEmptyEntries).Last();
        name = name.Split('<', StringSplitOptions.RemoveEmptyEntries)[0];
        CheckName(name, k.Compund.Compound.Location!, root, CaseMatch.CamelCase, ValidTypesNames, "class/struct");
    }

    private void CheckFunction(string root, memberdefType mem)
    {
        if (mem.Templateparamlist != null)
        {
            CheckTemplateArguments(root, mem.Location, mem.Templateparamlist.param);
        }
    }

    private void CheckEnum(string root, memberdefType mem)
    {
        CheckName(mem.Name, mem.Location, root, CaseMatch.CamelCase, NoValidNames, "enum");
        foreach(var e in mem.Enumvalue)
        {
            CheckName(e.name, mem.Location, root, CaseMatch.LowerSnakeCase, NoValidNames, "enum value");
        }
    }

    private void CheckTypedef(string root, memberdefType mem)
    {
        CheckName(mem.Name, mem.Location, root, CaseMatch.CamelCase, NoValidNames, "typedef");
    }

    private void CheckDefine(string root, memberdefType mem)
    {
        CheckName(mem.Name, mem.Location, root, CaseMatch.CamelCase, NoValidNames, "define");
    }

    private void CheckTemplateArguments(string root, locationType location, paramType[] pp)
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
                        CheckName(cmds[1], location, root, CaseMatch.CamelCase, ValidTypesNames, "template param");
                    }
                }
            }
        }
    }
}
