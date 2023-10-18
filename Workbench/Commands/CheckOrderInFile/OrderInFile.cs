using Spectre.Console;
using Workbench.Doxygen;
using static Workbench.Doxygen.LinkedTextType;

namespace Workbench.Commands.CheckOrderInFile;

internal static class OrderInFile
{
    internal static int ClassifyClass(string file, string class_name)
    {
        var parsed = Doxygen.Doxygen.ParseIndex(file);

        var total = 0;
        var matches = 0;

        foreach (var k in DoxygenUtils.AllClasses(parsed))
        {
            total += 1;
            if (!k.CompoundName.Contains(class_name))
            {
                continue;
            }

            AnsiConsole.MarkupLineInterpolated($"[blue]{k.CompoundName}[/]");
            matches += 1;

            foreach (var member in DoxygenUtils.AllMembersForAClass(k))
            {
                AnsiConsole.MarkupLineInterpolated($"{DoxygenUtils.MemberToString(member)} - {Classify(k, member).Name}");
            }
        }

        AnsiConsole.MarkupLineInterpolated($"[blue]{matches} / {total}[/] classes were matched");

        return 0;
    }

    public static int Run(Printer printer, string file, string root)
    {
        var parsed = Doxygen.Doxygen.ParseIndex(file);

        var checks = 0;
        var fails = new List<CheckError>();

        foreach (var k in DoxygenUtils.AllClasses(parsed))
        {
            checks += 1;
            var err = CheckClass(k, root);
            if (err != null)
            {
                fails.Add(err);
            }
        }

        foreach (var f in fails
            .OrderBy(x => x.Class.Location!.File)
            .ThenByDescending(x => x.Class.Location!.Line)
            )
        {
            PrintError(printer, f.Class, f.PrimaryFile, f.SecondaryFile, f.ErrorMessage);
        }

        var number_of_files = fails.DistinctBy(x => x.Class.Location!.File).Count();

        AnsiConsole.MarkupLineInterpolated($"[blue]{checks - fails.Count} / {checks}[/] classes were accepted");
        AnsiConsole.MarkupLineInterpolated($"Found [red]{fails.Count}[/] badly ordered classes in [red]{number_of_files}[/] files");

        if (checks > 0 && fails.Count == 0) { return 0; }
        else { return -1; }
    }

    private record CheckError(CompoundDef Class, string PrimaryFile, string SecondaryFile, string ErrorMessage);

    private static CheckError? CheckClass(CompoundDef k, string root)
    {
        // k.name
        var members = DoxygenUtils.AllMembersForAClass(k).ToArray();

        NamedOrder? last_class = null;
        MemberDefinitionType? last_member = null;

        foreach (var member in members.OrderBy(x => x.Location?.Line ?? -1))
        {
            var new_class = Classify(k, member);
            if (last_class != null)
            {
                if (member.Location?.File != last_member!.Location?.File)
                {
                    throw new Exception("invalid data");
                }

                if (last_class.Order > new_class.Order)
                {
                    var primary_file = DoxygenUtils.LocationToString(member.Location, root);
                    var secondary_file = DoxygenUtils.LocationToString(last_member.Location, root);
                    var error_message = $"Members for {k.CompoundName} are ordered badly, can't go from {last_class.Name} ({DoxygenUtils.MemberToString(last_member!)}) to {new_class.Name} ({DoxygenUtils.MemberToString(member)})!";
                    return new CheckError(k, primary_file, secondary_file, error_message);
                }
            }
            last_class = new_class;
            last_member = member;
        }

        return null;
    }

    private static void PrintError(
        Printer printer, CompoundDef k, string primary_file, string secondary_file, string error_message)
    {
        var members = DoxygenUtils.AllMembersForAClass(k).ToArray();
        printer.Error(primary_file, error_message);
        AnsiConsole.WriteLine($"{secondary_file}: From here");

        AnsiConsole.WriteLine("Something better could be:");
        AnsiConsole.WriteLine("");

        // print better order
        PrintSuggestedOrder(k, members);
        AnsiConsole.WriteLine("");
        AnsiConsole.WriteLine("");
    }

    private static void PrintSuggestedOrder(CompoundDef k, IEnumerable<MemberDefinitionType> members)
    {
        var sorted = members
            .GroupBy(x => Classify(k, x), (group, items) => (group, items.ToArray()))
            .OrderBy(x => x.group.Order)
            ;
        foreach (var (c, items) in sorted)
        {
            AnsiConsole.MarkupLineInterpolated($"// [blue]{c.Name}[/]");
            foreach (var it in items)
            {
                AnsiConsole.MarkupLineInterpolated($"  [green]{DoxygenUtils.MemberToString(it)}[/]");
            }
            AnsiConsole.WriteLine("");
        }
    }

    private record NamedOrder(string Name, int Order)
    {
        internal NamedOrder Specify(string visibility, int order_change)
        {
            return new NamedOrder($"{visibility} {Name}", Order + order_change);
        }
    }

    private static readonly NamedOrder typedefs = new("typedefs", -40);
    private static readonly NamedOrder friends = new("friends", -30);
    private static readonly NamedOrder enums = new("enums", -20);
    private static readonly NamedOrder vars = new("variables", -10);
    private static readonly NamedOrder constructors = new("constructors", 0);
    private static readonly NamedOrder defaults = new("defaults", 1);
    private static readonly NamedOrder deleted = new("deleted", 2);
    private static readonly NamedOrder creators = new("creators", 5);
    private static readonly NamedOrder manipulator = new("manipulators", 10);
    private static readonly NamedOrder calculators = new("calculators", 15);
    private static readonly NamedOrder accessor = new("acessors", 20);
    private static readonly NamedOrder operators = new("operators", 30);
    private static readonly NamedOrder utils = new("utils", 35);
    private static readonly NamedOrder overrides = new("override", 37);
    private static readonly NamedOrder virtuals = new("virtual", 40);
    private static readonly NamedOrder pure_virtuals = new("pure virtual", 45);

    private static NamedOrder Classify(CompoundDef k, MemberDefinitionType m)
    {
        var r = SubClassify(k, m);
        return m.Protection switch
        {
            DoxProtectionKind.Public => r.Specify("Public", 0),
            DoxProtectionKind.Protected => r.Specify("Protected", 1000),
            DoxProtectionKind.Private => r.Specify("Private", 2000),
            DoxProtectionKind.Package => r.Specify("Package", 3000),
            _ => throw new NotImplementedException(),
        };
    }

    private static NamedOrder SubClassify(CompoundDef k, MemberDefinitionType m)
    {
        if (m.ArgsString?.EndsWith("=delete") ?? false)
        {
            return deleted;
        }

        if (m.ArgsString?.EndsWith("=default") ?? false)
        {
            if (DoxygenUtils.IsConstructorOrDestructor(m) && m.Param.Length == 0)
            {
                // empty constructors and destructors are not-default even when they are defaulted
                return constructors;
            }
            else
            {
                return defaults;
            }
        }

        if (m.Name.StartsWith("operator"))
        {
            return operators;
        }

        if (m.Virtual != null && m.Virtual != DoxVirtualKind.NonVirtual)
        {
            if (DoxygenUtils.IsFunctionOverride(m))
            {
                return overrides;
            }

            if (m.Virtual == DoxVirtualKind.Virtual && DoxygenUtils.IsConstructorOrDestructor(m) == false)
            {
                return virtuals;
            }

            if (m.Virtual == DoxVirtualKind.PureVirtual)
            {
                return pure_virtuals;
            }
        }

        if (m.Kind == DoxMemberKind.Function)
        {
            if (DoxygenUtils.IsFunctionOverride(m))
            {
                return overrides;
            }

            if (m.IsStatic == DoxBool.Yes)
            {
                // undecorated return AND that is a ref AND that references the current class
                if (m.Type?.Nodes.Any(node => node is Ref ret && ret.Value.RefId == k.Id) ?? false)
                {
                    return creators;
                }
                else
                {
                    return utils;
                }
            }
            else if (DoxygenUtils.IsConstructorOrDestructor(m))
            {
                // constructor
                return constructors;
            }
            else if (m.IsConst == DoxBool.Yes || m.IsConstexpr == DoxBool.Yes)
            {
                if (m.Param.Length > 0)
                {
                    return calculators;
                }
                else
                {
                    return accessor;
                }
            }
            else
            {
                return manipulator;
            }
        }

        if (m.Kind == DoxMemberKind.Variable)
        {
            return vars;
        }

        if (m.Kind == DoxMemberKind.Typedef)
        {
            return typedefs;
        }

        if (m.Kind == DoxMemberKind.Enum)
        {
            return enums;
        }

        if (m.Kind == DoxMemberKind.Friend)
        {
            return friends;
        }

        return new NamedOrder("everything", 0);
    }
}
