using Spectre.Console;
using Workbench.Doxygen.Compound;
using static Workbench.Doxygen.Compound.linkedTextType;

namespace Workbench;

internal static class OrderInFile
{
    internal static int ClassifyClass(string file, string className)
    {
        var parsed = Doxygen.Doxygen.ParseIndex(file);

        var total = 0;
        var matches = 0;

        foreach (var k in DoxygenUtils.AllClasses(parsed))
        {
            total += 1;
            if (!k.CompoundName.Contains(className))
            {
                continue;
            }

            AnsiConsole.MarkupLineInterpolated($"[blue]{k.CompoundName}[/]");
            matches += 1;

            foreach(var member in DoxygenUtils.AllMembersForAClass(k))
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

        foreach(var f in fails
            .OrderBy(x => x.Class.Location!.file)
            .ThenByDescending(x => x.Class.Location!.line)
            )
        {
            PrintError(printer, f.Class, f.PrimaryFile, f.SecondaryFile, f.ErrorMessage);
        }

        var numberOfFiles = fails.DistinctBy(x => x.Class.Location!.file).Count();

        AnsiConsole.MarkupLineInterpolated($"[blue]{checks - fails.Count} / {checks}[/] classes were accepted");
        AnsiConsole.MarkupLineInterpolated($"Found [red]{fails.Count}[/] badly ordered classes in [red]{numberOfFiles}[/] files");

        if (checks > 0 && fails.Count == 0) { return 0; }
        else { return -1; }
    }

    private record CheckError(CompoundDef Class, string PrimaryFile, string SecondaryFile, string ErrorMessage);

    private static CheckError? CheckClass(CompoundDef k, string root)
    {
        // k.name
        var members = DoxygenUtils.AllMembersForAClass(k).ToArray();

        NamedOrder? lastClass = null;
        memberdefType? lastMember = null;

        foreach (var member in members.OrderBy(x => x.Location?.line ?? -1))
        {
            var newClass = Classify(k, member);
            if (lastClass != null)
            {
                if (member.Location?.file != lastMember!.Location?.file)
                {
                    throw new Exception("invalid data");
                }

                if (lastClass.Order > newClass.Order)
                {
                    var primaryFile = DoxygenUtils.LocationToString(member.Location, root);
                    var secondaryFile = DoxygenUtils.LocationToString(lastMember.Location, root);
                    var errorMessage = $"Members for {k.CompoundName} are ordered badly, can't go from {lastClass.Name} ({DoxygenUtils.MemberToString(lastMember!)}) to {newClass.Name} ({DoxygenUtils.MemberToString(member)})!";
                    return new CheckError(k, primaryFile, secondaryFile, errorMessage);
                }
            }
            lastClass = newClass;
            lastMember = member;
        }

        return null;
    }

    private static void PrintError(Printer printer, CompoundDef k, string primaryFile, string secondaryFile, string errorMessage)
    {
        var members = DoxygenUtils.AllMembersForAClass(k).ToArray();
        printer.Error(primaryFile, errorMessage);
        AnsiConsole.WriteLine($"{secondaryFile}: From here");

        AnsiConsole.WriteLine("Something better could be:");
        AnsiConsole.WriteLine("");

        // print better order
        PrintSuggestedOrder(k, members);
        AnsiConsole.WriteLine("");
        AnsiConsole.WriteLine("");
    }

    private static void PrintSuggestedOrder(CompoundDef k, IEnumerable<memberdefType> members)
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
        internal NamedOrder Specify(string visibility, int orderChange)
        {
            return new NamedOrder($"{visibility} {Name}", Order + orderChange);
        }
    }

    private static readonly NamedOrder Typedefs = new("typedefs", -40);
    private static readonly NamedOrder Friends = new("friends", -30);
    private static readonly NamedOrder Enums = new("enums", -20);
    private static readonly NamedOrder Vars = new("variables", -10);
    private static readonly NamedOrder Constructors = new("constructors", 0);
    private static readonly NamedOrder Defaults = new("defaults", 1);
    private static readonly NamedOrder Deleted = new("deleted", 2);
    private static readonly NamedOrder Creators = new("creators", 5);
    private static readonly NamedOrder Manipulator = new("manipulators", 10);
    private static readonly NamedOrder Calculators = new("calculators", 15);
    private static readonly NamedOrder Accessor = new("acessors", 20);
    private static readonly NamedOrder Operators = new("operators", 30);
    private static readonly NamedOrder Utils = new("utils", 35);
    private static readonly NamedOrder Overrides = new("override", 37);
    private static readonly NamedOrder Virtuals = new("virtual", 40);
    private static readonly NamedOrder PureVirtuals = new("pure virtual", 45);

    private static NamedOrder Classify(CompoundDef k, memberdefType m)
    {
        var r = SubClassify(k, m);
        return m.Prot switch
        {
            DoxProtectionKind.Public => r.Specify("Public", 0),
            DoxProtectionKind.Protected => r.Specify("Protected", 1000),
            DoxProtectionKind.Private => r.Specify("Private", 2000),
            DoxProtectionKind.Package => r.Specify("Package", 3000),
            _ => throw new NotImplementedException(),
        };
    }

    private static NamedOrder SubClassify(CompoundDef k, memberdefType m)
    {
        if(m.Argsstring?.EndsWith("=delete") ?? false)
        {
            return Deleted;
        }

        if (m.Argsstring?.EndsWith("=default") ?? false)
        {
            if(DoxygenUtils.IsConstructorOrDestructor(m) && m.Param.Length == 0)
            {
                // empty constructors and destructors are not-default even when they are defaulted
                return Constructors;
            }
            else
            {
                return Defaults;
            }
        }

        if (m.Name.StartsWith("operator"))
        {
            return Operators;
        }

        if(m.Virt != null && m.Virt != DoxVirtualKind.NonVirtual)
        {
            if (DoxygenUtils.IsFunctionOverride(m))
            {
                return Overrides;
            }

            if (m.Virt == DoxVirtualKind.Virtual && DoxygenUtils.IsConstructorOrDestructor(m) == false)
            {
                return Virtuals;
            }

            if (m.Virt == DoxVirtualKind.PureVirtual)
            {
                return PureVirtuals;
            }
        }

        if (m.Kind == DoxMemberKind.Function)
        {
            if (DoxygenUtils.IsFunctionOverride(m))
            {
                return Overrides;
            }

            if (m.Static == DoxBool.Yes)
            {
                // undecorated return AND that is a ref AND that references the current class
                if (m.Type?.Nodes.Any(node => node is Ref ret && ret.Value.refid == k.Id) ?? false)
                {
                    return Creators;
                }
                else
                {
                    return Utils;
                }
            }
            else if (DoxygenUtils.IsConstructorOrDestructor(m))
            {
                // constructor
                return Constructors;
            }
            else if (m.Const == DoxBool.Yes || m.Constexpr == DoxBool.Yes)
            {
                if (m.Param.Length > 0)
                {
                    return Calculators;
                }
                else
                {
                    return Accessor;
                }
            }
            else
            {
                return Manipulator;
            }
        }

        if (m.Kind == DoxMemberKind.Variable)
        {
            return Vars;
        }

        if(m.Kind == DoxMemberKind.Typedef)
        {
            return Typedefs;
        }

        if (m.Kind == DoxMemberKind.Enum)
        {
            return Enums;
        }

        if(m.Kind == DoxMemberKind.Friend)
        {
            return Friends;
        }

        return new NamedOrder("everything", 0);
    }
}
