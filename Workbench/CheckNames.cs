using Spectre.Console;
using Workbench.Config;
using Workbench.Doxygen.Compound;
using Workbench.Doxygen.Index;
using Workbench.Utils;

namespace Workbench;


internal class CheckNames
{
    readonly CheckNamesFile file;

    private bool ValidTypeNames(string name)
    {
        return file.AcceptedTypes.Contains(name);
    }
    
    private bool IsValidMethodName(string name, bool is_cpp)
    {
        if (is_cpp)
        {
            if(name.StartsWith("operator\"")) { return true; }
            if (accepted_cpp_names.Contains(name)) { return true; }
        }
        return file.AcceptedFunctions.Contains(name);
    }
    private static readonly HashSet<string> accepted_cpp_names = new()
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


    int names_checked = 0;
    int errors_detected = 0;
    private readonly Printer printer;

    public CheckNames(Printer printer, CheckNamesFile file)
    {
        this.printer = printer;
        this.file = file;

        string cwd = Environment.CurrentDirectory;
    }

    internal bool CheckName(string name, LocationType? loc, Func<string, bool> check_case,
        Func<string, bool> valid_name, string source)
    {
        // doxygen hack
        if (name.StartsWith('@')) { return true; }

        names_checked += 1;
        if(valid_name(name)) { return true; }

        if (check_case(name) == false)
        {
            ReportError(loc, $"{name} is a invalid name for {source}");
            return false;
        }

        return true;
    }

    readonly List<Fail> fails = new();
    record Fail(LocationType Location, string ErrorMessage);

    private void ReportError(LocationType? loc, string error)
    {
        if (loc == null || loc.File == "[generated]") { return; }

        errors_detected += 1;
        fails.Add(new(loc, error));
    }

    private void PrintErrors(string root)
    {
        foreach (var f in fails
            .OrderBy(x => x.Location.File)
            .ThenByDescending(x => x.Location!.Line)
            )
        {
            printer.Error(DoxygenUtils.LocationToString(f.Location, root), f.ErrorMessage);
        }
    }

    internal static int Run(Printer printer, string doxygen_xml, string root)
    {
        var file = CheckNamesFile.LoadFromDirectoryOrNull(printer);
        if (file == null)
        {
            return -1;
        }

        var parsed = Doxygen.Doxygen.ParseIndex(doxygen_xml);

        CheckNames runner = new(printer, file);
        foreach (var k in DoxygenUtils.AllClasses(parsed))
        {
            runner.CheckClass(k);
        }

        foreach(var k in parsed.Compounds.Where(x => x.Kind == CompoundKind.File))
        {
            runner.CheckFile(k);
        }

        foreach (var k in parsed.Compounds.Where(x => x.Kind == CompoundKind.Namespace))
        {
            runner.CheckNamespace(k);
        }

        runner.PrintErrors(root);
        runner.WriteFunctionMatchLog();

        AnsiConsole.MarkupLineInterpolated($"Found [red]{runner.errors_detected}[/] issues in [blue]{runner.names_checked}[/] names");

        return runner.errors_detected > 0 ? -1 : 0;
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

    private void CheckNamespace(CompoundType k)
    {
        CheckName(RemoveNamespace(k.Name), k.DoxygenFile.FirstCompound.Location!, CaseMatch.IsLowerSnakeCase, NoValidNames, "namespace");
        CheckSectionDefs(k);
    }

    private void CheckFile(CompoundType k)
    {
        // check namespaces?
        // foreach (var n in k.Compund.Compound.Innernamespace)

        CheckSectionDefs(k);
    }

    private void CheckSectionDefs(CompoundType k)
    {
        foreach (var d in k.DoxygenFile.FirstCompound.SectionDefs)
        {
            foreach (var m in d.MemberDef)
            {
                switch (m.Kind)
                {
                    case DoxMemberKind.Variable:
                        CheckName(m.Name, m.Location, CaseMatch.IsLowerSnakeCase, NoValidNames, "variable");
                        break;
                    case DoxMemberKind.Define:
                        CheckName(m.Name, m.Location, CaseMatch.IsUpperSnakeCase, NoValidNames, "define");
                        break;
                    case DoxMemberKind.Typedef:
                        CheckName(m.Name, m.Location, CaseMatch.IsCamelCase, ValidTypeNames, "typedef");
                        break;
                    case DoxMemberKind.Enum:
                        CheckEnum(m);
                        break;
                    case DoxMemberKind.Function:
                        CheckFunction(m);
                        CheckFunctionName(k.DoxygenFile.FirstCompound, m, is_function: true);
                        break;
                    default:
                        throw new Exception("Unhandled type");
                }
            }
        }
    }

    readonly Dictionary<string, int> counts = new ();
    void AddCount(string name)
    {
        if (counts.TryGetValue(name, out int value) == false)
        {
            value = 0;
        }
        value += 1;
        counts[name] = value;
    }

    private void CheckFunctionName(CompoundDef c, MemberDefinitionType mem, bool is_function)
    {
        var source = is_function ? "function" : "method";
        
        if (IsValidMethodName(mem.Name, c.Language == DoxLanguage.Cpp))
        {
            return;
        }

        var member_name = RemoveTemplateArguments(mem.Name);

        if (false == CheckName(member_name, mem.Location, CaseMatch.IsLowerSnakeCase, _ => true, source))
        {
            return;
        }

        if (mem.Location != null && file.IgnoredFiles.Contains(mem.Location.File))
        {
            return;
        }

        var entries = member_name.Split('_', StringSplitOptions.TrimEntries)
            .SkipWhile(file.KnownFunctionPrefixes.Contains)
            .ToArray()
            ;
        var first_name = entries[0].ToLower()
            // for a name get360 the verb should be just get
            .TrimEnd("0123456789".ToCharArray());

        if(entries.Length > 0)
        {
            bool add = true;

            if(file.KnownFunctionVerbs.Contains(first_name) == false)
            {
                if (file.BadFunctionVerbs.TryGetValue(first_name, out var suggested_replacements))
                {
                    var message = StringListCombiner.EnglishOr().Combine(suggested_replacements);
                    var are = suggested_replacements.Length ==1 ? "is" : "are";
                    this.ReportError(mem.Location, $"{first_name} is not a recomended verb for {member_name}: suggestions {are}: {message}");
                }
                else
                {
                    this.ReportError(mem.Location, $"{first_name} is not a known verb for {member_name}");
                }
            }

            if(add)
            {
                AddCount(first_name);
            }
        }
    }

    private void CheckClass(CompoundDef c)
    {
        if(c.Templateparamlist != null)
        {
            CheckTemplateArguments(c.Location!, c.Templateparamlist.Params);
        }
        foreach (var mem in c.SectionDefs.SelectMany(x => x.MemberDef))
        {
            switch (mem.Kind)
            {
                case Doxygen.Compound.DoxMemberKind.Define:
                    CheckDefine(mem);
                    break;
                case Doxygen.Compound.DoxMemberKind.Typedef:
                    CheckTypedef(mem);
                    break;
                case Doxygen.Compound.DoxMemberKind.Enum:
                    CheckEnum(mem);
                    break;
                case Doxygen.Compound.DoxMemberKind.Variable:
                    CheckName(mem.Name, mem.Location, CaseMatch.IsLowerSnakeCase, NoValidNames, "member variables");
                    break;
                case Doxygen.Compound.DoxMemberKind.Function:
                    CheckFunction(mem);
                    if (DoxygenUtils.IsConstructorOrDestructor(mem) == false &&
                        DoxygenUtils.IsFunctionOverride(mem) == false)
                    {
                        CheckFunctionName(c, mem, is_function: false);
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

        string name = RemoveNamespace(c.CompoundName);
        name = RemoveTemplateArguments(name);
        CheckName(name, c.Location!, CaseMatch.IsCamelCase, ValidTypeNames, "class/struct");
    }

    

    private static string RemoveTemplateArguments(string name)
    {
        return name.Split('<', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
    }

    private static string RemoveNamespace(string name)
    {
        return name.Split("::", StringSplitOptions.RemoveEmptyEntries).Last().Trim();
    }

    private void CheckFunction(MemberDefinitionType mem)
    {
        if (mem.TemplateParamList != null && mem.Location != null)
        {
            CheckTemplateArguments(mem.Location, mem.TemplateParamList.Params);
        }
    }

    private void CheckEnum(MemberDefinitionType mem)
    {
        CheckName(mem.Name, mem.Location, CaseMatch.IsCamelCase, NoValidNames, "enum");
        foreach(var e in mem.EnumValues)
        {
            CheckName(e.Name, mem.Location, CaseMatch.IsLowerSnakeCase, NoValidNames, "enum value");
        }
    }

    private void CheckTypedef(MemberDefinitionType mem)
    {
        CheckName(mem.Name, mem.Location, CaseMatch.IsCamelCase, NoValidNames, "typedef");
    }

    private void CheckDefine(MemberDefinitionType mem)
    {
        CheckName(mem.Name, mem.Location, CaseMatch.IsCamelCase, NoValidNames, "define");
    }

    private void CheckTemplateArguments(LocationType location, ParamType[] pp)
    {
        foreach (var t in pp)
        {
            var val = t.Type!.ToString();
            // foreach (var n in t.type!.Nodes)
            {
                //if (n is linkedTextType.Text text)
                {
                    //var val = text.Value;
                    // if (val == "typename..." || val == "typename") { continue; }
                    var cmds = val.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (cmds.Length == 2 && cmds[0] == "typename")
                    {
                        CheckTemplateParamName(location, cmds[1]);
                    }
                }
            }
        }
    }

    private void CheckTemplateParamName(LocationType location, string template_name)
    {
        // the name is a single char and it's uppercase it's valid
        if (template_name.Length == 1 && template_name[0] == template_name.ToUpper()[0]) { return; }

        // otherwise it must follow the "template name"
        CheckName(template_name, location, CaseMatch.IsTemplateName, ValidTypeNames, "template param");

        if(template_name.EndsWith("Function") || template_name.EndsWith("Fun"))
        {
            ReportError(location, $"End template arguments representing functions with Func instead of Function or Fun for {template_name}");
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
