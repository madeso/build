using System.Text.RegularExpressions;
using Spectre.Console;
using Workbench.Config;
using Workbench.Shared;
using Workbench.Shared.Doxygen;
using Workbench.Shared.Doxygen.Compound;
using Workbench.Shared.Doxygen.Index;
using CompoundType = Workbench.Shared.Doxygen.CompoundType;

namespace Workbench.Commands.CheckNames;


internal static partial class CaseMatch
{
    public static bool IsLowerSnakeCase(string name) => lower_snake_case_regex.IsMatch(name);
    private static readonly Regex lower_snake_case_regex = GenerateLowerSnakeCase();
    [GeneratedRegex("^[a-z]([a-z0-9]*(_[a-z0-9]+)*)?$", RegexOptions.Compiled)]
    private static partial Regex GenerateLowerSnakeCase();

    public static bool IsUpperSnakeCase(string name) => upper_snake_case_regex.IsMatch(name);
    private static readonly Regex upper_snake_case_regex = GenerateUpperSnakeCase();
    [GeneratedRegex("^[A-Z]([A-Z0-9]*(_[A-Z0-9]+)*)?$", RegexOptions.Compiled)]
    private static partial Regex GenerateUpperSnakeCase();

    public static bool IsCamelCase(string name) =>
        // handle special case where all characters are uppercase, this is not valid
        name == name.ToUpperInvariant() ? false
            : camel_case_regex.IsMatch(name);
    private static readonly Regex camel_case_regex = GenerateCamelCase();
    [GeneratedRegex("^[A-Z]([a-z0-9]+([A-Z0-9][a-z0-9]*)*)?$", RegexOptions.Compiled)]
    private static partial Regex GenerateCamelCase();

    public static bool IsTemplateName(string name) => template_name_regex.IsMatch(name);
    private static readonly Regex template_name_regex = GenerateTemplateName();
    [GeneratedRegex("^T[A-Z][a-z0-9]+([A-Z0-9][a-z0-9]+)*$", RegexOptions.Compiled)]
    private static partial Regex GenerateTemplateName();

}

internal class CheckNamesRunner
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


    private int names_checked = 0;
    private int errors_detected = 0;
    private readonly Log log;

    public CheckNamesRunner(Log log, CheckNamesFile file)
    {
        this.log = log;
        this.file = file;
    }

    internal bool CheckName(string name, locationType loc, Func<string, bool> check_case,
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
    record Fail(locationType Location, string ErrorMessage);

    private void ReportError(locationType loc, string error)
    {
        if (loc == null || loc.file == "[generated]") { return; }

        errors_detected += 1;
        fails.Add(new(loc, error));
    }

    private void PrintErrors(Vfs vfs, Dir root)
    {
        foreach (var f in fails
            .OrderBy(x => x.Location.file)
            .ThenByDescending(x => x.Location!.line)
            )
        {
            log.Error(DoxygenUtils.LocationToString(vfs, f.Location, root), f.ErrorMessage);
        }
    }

    internal static int Run(Vfs vfs, Dir cwd, Log log, Dir doxygen_xml, Dir root)
    {
        var file = CheckNamesFile.LoadFromDirectoryOrNull(vfs, cwd, log);
        if (file == null)
        {
            return -1;
        }

        var parsed = Doxygen.ParseIndex(doxygen_xml);

        CheckNamesRunner runner = new(log, file);
        foreach (var k in DoxygenUtils.AllClasses(parsed))
        {
            runner.CheckClass(k);
        }

        foreach(var k in parsed.Compounds.Where(x => x.Kind == CompoundKind.file))
        {
            runner.CheckFile(k);
        }

        foreach (var k in parsed.Compounds.Where(x => x.Kind == CompoundKind.@namespace))
        {
            runner.CheckNamespace(k);
        }

        runner.PrintErrors(vfs, root);
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
        CheckName(RemoveNamespace(k.name), k.DoxygenFile.FirstCompound.location!, CaseMatch.IsLowerSnakeCase, NoValidNames, "namespace");
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
            foreach (var m in d.memberdef)
            {
                switch (m.kind)
                {
                    case DoxMemberKind.variable:
                        CheckName(m.name, m.location, CaseMatch.IsLowerSnakeCase, NoValidNames, "variable");
                        break;
                    case DoxMemberKind.define:
                        CheckName(m.name, m.location, CaseMatch.IsUpperSnakeCase, NoValidNames, "define");
                        break;
                    case DoxMemberKind.typedef:
                        CheckName(m.name, m.location, CaseMatch.IsCamelCase, ValidTypeNames, "typedef");
                        break;
                    case DoxMemberKind.@enum:
                        CheckEnum(m);
                        break;
                    case DoxMemberKind.function:
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

    private void CheckFunctionName(compounddefType c, memberdefType mem, bool is_function)
    {
        var source = is_function ? "function" : "method";
        
        if (IsValidMethodName(mem.name, c.language == DoxLanguage.C1))
        {
            return;
        }

        var member_name = RemoveTemplateArguments(mem.name);

        if (false == CheckName(member_name, mem.location, CaseMatch.IsLowerSnakeCase, _ => true, source))
        {
            return;
        }

        if (mem.location != null && file.IgnoredFiles.Contains(mem.location.file))
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
                    var message = StringListCombiner.EnglishOr().CombineArray(suggested_replacements);
                    var are = suggested_replacements.Length ==1 ? "is" : "are";
                    ReportError(mem.location, $"{first_name} is not a recommended verb for {member_name}: suggestions {are}: {message}");
                }
                else
                {
                    ReportError(mem.location, $"{first_name} is not a known verb for {member_name}");
                }
            }

            if(add)
            {
                AddCount(first_name);
            }
        }
    }

    private void CheckClass(compounddefType c)
    {
        if(c.templateparamlist.Length != !0)
        {
            CheckTemplateArguments(c.location, c.templateparamlist);
        }
        foreach (var mem in c.sectiondef.SelectMany(x => x.memberdef))
        {
            switch (mem.kind)
            {
                case DoxMemberKind.define:
                    CheckDefine(mem);
                    break;
                case DoxMemberKind.typedef:
                    CheckTypedef(mem);
                    break;
                case DoxMemberKind.@enum:
                    CheckEnum(mem);
                    break;
                case DoxMemberKind.variable:
                    CheckName(mem.name, mem.location, CaseMatch.IsLowerSnakeCase, NoValidNames, "member variables");
                    break;
                case DoxMemberKind.function:
                    CheckFunction(mem);
                    if (DoxygenUtils.IsConstructorOrDestructor(mem) == false &&
                        DoxygenUtils.IsFunctionOverride(mem) == false)
                    {
                        CheckFunctionName(c, mem, is_function: false);
                    }
                    break;
                case DoxMemberKind.friend:
                    // nop
                    break;
                default:
                    throw new Exception("unhandled type");
            }
        }

        // todo(Gustav): check template parameter on current klass

        string name = RemoveNamespace(c.compoundname);
        name = RemoveTemplateArguments(name);
        CheckName(name, c.location!, CaseMatch.IsCamelCase, ValidTypeNames, "class/struct");
    }

    

    private static string RemoveTemplateArguments(string name)
    {
        return name.Split('<', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
    }

    private static string RemoveNamespace(string name)
    {
        return name.Split("::", StringSplitOptions.RemoveEmptyEntries).Last().Trim();
    }

    private void CheckFunction(memberdefType mem)
    {
        if (mem.templateparamlist.Length > 0)
        {
            CheckTemplateArguments(mem.location, mem.templateparamlist);
        }
    }

    private void CheckEnum(memberdefType mem)
    {
        CheckName(mem.name, mem.location, CaseMatch.IsCamelCase, NoValidNames, "enum");
        foreach(var e in mem.enumvalue)
        {
            CheckName(e.name, mem.location, CaseMatch.IsLowerSnakeCase, NoValidNames, "enum value");
        }
    }

    private void CheckTypedef(memberdefType mem)
    {
        CheckName(mem.name, mem.location, CaseMatch.IsCamelCase, NoValidNames, "typedef");
    }

    private void CheckDefine(memberdefType mem)
    {
        CheckName(mem.name, mem.location, CaseMatch.IsCamelCase, NoValidNames, "define");
    }

    private void CheckTemplateArguments(locationType location, paramType[] pp)
    {
        foreach (var t in pp)
        {
            var val = t.type.ToString();
            // foreach (var n in t.type!.Nodes)
            {
                //if (n is linkedTextType.Text text)
                {
                    //var val = text.Value;
                    // if (val == "typename..." || val == "typename") { continue; }
                    var cmds = val.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (cmds is ["typename", _])
                    {
                        CheckTemplateParamName(location, cmds[1]);
                    }
                }
            }
        }
    }

    private void CheckTemplateParamName(locationType location, string template_name)
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

    internal static int HandleInit(Vfs vfs, Dir cwd, Log print, bool overwrite)
    {
        var data = new CheckNamesFile();
        data.AcceptedFunctions.Add("some function or method name");
        data.AcceptedTypes.Add("some struct or class name");

        return ConfigFile.WriteInit(vfs, print, overwrite, CheckNamesFile.GetBuildDataPath(cwd), data);
    }
}
