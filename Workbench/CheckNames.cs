using Spectre.Console;
using System.Text;
using System.Xml.Linq;
using Workbench.Config;
using Workbench.Doxygen.Compound;
using Workbench.Doxygen.Index;
using Workbench.Utils;

namespace Workbench;


internal class CheckNames
{
    CheckNamesFile file;

    private bool ValidTypeNames(string name)
    {
        return file.AcceptedTypes.Contains(name);
    }
    
    private bool IsValidMethodName(string name, bool isCpp)
    {
        if (isCpp)
        {
            if(name.StartsWith("operator\"")) { return true; }
            if (AcceptedCppNames.Contains(name)) { return true; }
        }
        return file.AcceptedFunctions.Contains(name);
    }
    private static readonly HashSet<string> AcceptedCppNames = new()
    {
        "operator+",
        "operator-",
        "operator*",
        "operator/",
        
        "operator<",
        "operator>",
        "operator<=",
        "operator>=",

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

    private static bool NoValidNames(string name) => false;


    int namesChecked = 0;
    int errorsDetected = 0;
    private readonly Printer printer;

    public CheckNames(Printer printer, CheckNamesFile file)
    {
        this.printer = printer;
        this.file = file;
    }

    internal void CheckName(string name, locationType loc, string root, Func<string, bool> checkCase, Func<string, bool> validName, string source)
    {
        if (name.StartsWith('@')) { return; }

        namesChecked += 1;
        if(validName(name)) { return; }

        if (checkCase(name) == false)
        {
            ReportError(loc, root, $"{name} is a invalid name for {source}");
        }
    }

    private void ReportError(locationType loc, string root, string error)
    {
        if (loc.file == "[generated]") { return; }

        errorsDetected += 1;
        printer.Error(DoxygenUtils.LocationToString(loc, root), error);
    }

    internal static int Run(Printer printer, string doxygenXml, string root)
    {
        var file = CheckNamesFile.LoadFromDirectoryOrNull(printer);
        if (file == null)
        {
            return -1;
        }

        var parsed = Doxygen.Doxygen.ParseIndex(doxygenXml);

        CheckNames runner = new(printer, file);
        foreach (var k in DoxygenUtils.AllClasses(parsed))
        {
            runner.CheckClass(root, k);
        }

        // var dict = parsed.compounds.ToDictionary(x=>x.refid);

        foreach(var k in parsed.compounds.Where(x => x.kind == CompoundKind.File))
        {
            runner.CheckFile(k, root);
        }

        foreach (var k in parsed.compounds.Where(x => x.kind == CompoundKind.Namespace))
        {
            runner.CheckNamespace(k, root);
        }

        runner.WriteFunctionMatchLog();

        AnsiConsole.MarkupLineInterpolated($"Found [red]{runner.errorsDetected}[/] issues in [blue]{runner.namesChecked}[/] names");

        return runner.errorsDetected > 0 ? -1 : 0;
    }

    private void WriteFunctionMatchLog()
    {
        var unknowns = counts.Where(c => file.KnownFunctionVerbs.Contains(c.Key) == false);
        foreach (var c in unknowns.OrderByDescending(x => x.Value))
        {
            AnsiConsole.MarkupLineInterpolated($"Detected [red]{c.Value}[/] unknown verbs [blue]{c.Key}[/] in function names");
        }

        var unmatched = file.KnownFunctionVerbs.Where(x => counts.ContainsKey(x) == false);
        foreach (var c in unmatched)
        {
            AnsiConsole.MarkupLineInterpolated($"Detected unmatched verbs [blue]{c}[/] in file");
        }
    }

    private void CheckNamespace(CompoundType k, string root)
    {
        CheckName(RemoveNamespace(k.name), k.Compund.Compound.Location!, root, CaseMatch.LowerSnakeCase, NoValidNames, "namespace");
        CheckSectionDefs(k, root);
    }

    private void CheckFile(CompoundType k, string root )
    {
        // check namespaces?
        // foreach (var n in k.Compund.Compound.Innernamespace)

        CheckSectionDefs(k, root);
    }

    private void CheckSectionDefs(CompoundType k, string root)
    {
        foreach (var d in k.Compund.Compound.Sectiondef)
        {
            foreach (var m in d.memberdef)
            {
                switch (m.Kind)
                {
                    case DoxMemberKind.Variable:
                        CheckName(m.Name, m.Location, root, CaseMatch.LowerSnakeCase, NoValidNames, "variable");
                        break;
                    case DoxMemberKind.Define:
                        CheckName(m.Name, m.Location, root, CaseMatch.UpperSnakeCase, NoValidNames, "define");
                        break;
                    case DoxMemberKind.Typedef:
                        CheckName(m.Name, m.Location, root, CaseMatch.CamelCase, ValidTypeNames, "typedef");
                        break;
                    case DoxMemberKind.Enum:
                        CheckEnum(root, m);
                        break;
                    case DoxMemberKind.Function:
                        CheckFunction(root, m);
                        CheckFunctionName(root, k.Compund.Compound, m, isFunction: true);
                        break;
                    default:
                        throw new Exception("Unhandled type");
                }
            }
        }
    }

    Dictionary<string, int> counts = new ();
    void AddCount(string name)
    {
        if (counts.TryGetValue(name, out int value) == false)
        {
            value = 0;
        }
        value += 1;
        counts[name] = value;
    }

    private void CheckFunctionName(string root, compounddefType c, memberdefType mem, bool isFunction)
    {
        var source = isFunction ? "function" : "method";
        var memName = RemoveTemplateArguments(mem.Name);

        if (IsValidMethodName(memName, c.Language == DoxLanguage.Cpp))
        {
            return;
        }

        var entries = memName.Split('_', StringSplitOptions.TrimEntries)
            .SkipWhile(file.KnownFunctionPrefixes.Contains)
            .ToArray()
            ;
        var firstName = entries[0].ToLower()
            // for a name get360 the verb should be just get
            .TrimEnd("0123456789".ToCharArray());

        if(entries.Length > 1)
        {
            bool add = true;

            if(file.KnownFunctionVerbs.Contains(firstName) == false)
            {
                if(file.BadFunctionVerbs.TryGetValue(firstName, out var suggestedReplacements))
                {
                    var message = StringListCombiner.EnglishOr().combine(suggestedReplacements);
                    var are = suggestedReplacements.Length ==1 ? "is" : "are";
                    this.ReportError(mem.Location, root, $"{firstName} is not a recomended verb for {memName}: suggestions {are}: {message}");
                }
                else
                {
                    this.ReportError(mem.Location, root, $"{firstName} is not a known verb for {memName}");
                }
            }

            if(add)
            {
                AddCount(firstName);
            }
        }

        CheckName(memName, mem.Location, root, CaseMatch.LowerSnakeCase,
                    _ => true, source);
    }

    private void CheckClass(string root, CompoundType k)
    {
        var c = k.Compund.Compound;
        if(c.Templateparamlist != null)
        {
            CheckTemplateArguments(root, c.Location!, c.Templateparamlist.param);
        }
        foreach (var mem in c.Sectiondef.SelectMany(x => x.memberdef))
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
                case Doxygen.Compound.DoxMemberKind.Variable:
                    CheckName(mem.Name, mem.Location, root, CaseMatch.LowerSnakeCase, NoValidNames, "member variables");
                    break;
                case Doxygen.Compound.DoxMemberKind.Function:
                    CheckFunction(root, mem);
                    if (DoxygenUtils.IsConstructorOrDestructor(mem) == false &&
                        DoxygenUtils.IsFunctionOverride(mem) == false)
                    {
                        CheckFunctionName(root, c, mem, isFunction: false);
                    }
                    break;
                case DoxMemberKind.Friend:
                    // nop
                    break;
                default:
                    throw new Exception("unhandled type");
            }
        }

        // todo(Gustav): check template parameter on current klass

        string name = RemoveNamespace(k.name);
        name = RemoveTemplateArguments(name);
        CheckName(name, c.Location!, root, CaseMatch.CamelCase, ValidTypeNames, "class/struct");
    }

    

    private static string RemoveTemplateArguments(string name)
    {
        return name.Split('<', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
    }

    private static string RemoveNamespace(string name)
    {
        return name.Split("::", StringSplitOptions.RemoveEmptyEntries).Last().Trim();
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
            var val = t.type!.ToString();
            // foreach (var n in t.type!.Nodes)
            {
                //if (n is linkedTextType.Text text)
                {
                    //var val = text.Value;
                    // if (val == "typename..." || val == "typename") { continue; }
                    var cmds = val.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (cmds.Length == 2 && cmds[0] == "typename")
                    {
                        CheckTemplateParamName(root, location, cmds[1]);
                    }
                }
            }
        }
    }

    private void CheckTemplateParamName(string root, locationType location, string templateName)
    {
        // the name is a single char and it's uppercase it's valid
        if (templateName.Length == 1 && templateName[0] == templateName.ToUpper()[0]) { return; }

        // otherwise it must follow the "template name"
        CheckName(templateName, location, root, CaseMatch.TemplateName, ValidTypeNames, "template param");

        if(templateName.EndsWith("Function") || templateName.EndsWith("Fun"))
        {
            ReportError(location, root, $"End template arguments representing functions with Func instead of Function or Fun for {templateName}");
        }
    }

    internal static int HandleInit(Printer print, bool overwrite)
    {
        var data = new CheckNamesFile();
        data.AcceptedFunctions.Add("some function or method name");
        data.AcceptedTypes.Add("some struct or class name");

        return ConfigFile.WriteInit(print, overwrite, CheckNamesFile.GetBuildDataPath(), data);
    }
}
