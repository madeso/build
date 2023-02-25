using Spectre.Console;
using Workbench.CompileCommands;
using Workbench.Doxygen.Compound;
using Workbench.Doxygen.Index;
using static Workbench.Doxygen.Compound.linkedTextType;

namespace Workbench;

internal static class OrderInFile
{
    internal static int ClassifyClass(Printer printer, string file, string className, string root)
    {
        var parsed = Doxygen.Doxygen.ParseIndex(file);

        int total = 0;
        int matches = 0;

        foreach (var k in DoxygenUtils.AllClasses(parsed))
        {
            total += 1;
            if(k.name.Contains(className))
            {
                AnsiConsole.MarkupLineInterpolated($"[blue]{k.name}[/]");
                matches += 1;

                foreach(var member in AllMethodsInClass(k))
                {
                    AnsiConsole.MarkupLineInterpolated($"{MemberToString(member)} - {Classify(k, member).Name}");
                }
            }
        }

        AnsiConsole.MarkupLineInterpolated($"[blue]{matches} / {total}[/] classes were matched");

        return 0;
    }

    public static int Run(Printer printer, string file, string root)
    {
        var parsed = Doxygen.Doxygen.ParseIndex(file);

        int checks = 0;
        List<CheckError> fails = new();

        foreach (var k in DoxygenUtils.AllClasses(parsed))
        {
            checks += 1;
            var err = CheckClass(printer, k, root);
            if (err != null)
            {
                fails.Add(err);
            }
        }

        foreach(var f in fails
            .OrderBy(x => x.Class.Compund.Compound.Location!.file)
            .ThenByDescending(x => x.Class.Compund.Compound.Location!.line)
            )
        {
            PrintError(printer, f.Class, f.PrimaryFile, f.SecondaryFile, f.ErrorMessage);
        }

        AnsiConsole.MarkupLineInterpolated($"[blue]{checks - fails.Count} / {checks}[/] classes were accepted");

        if (checks > 0 && fails.Count == 0) { return 0; }
        else { return -1; }
    }

    record CheckError(CompoundType Class, string PrimaryFile, string SecondaryFile, string ErrorMessage);

    private static CheckError? CheckClass(Printer printer, CompoundType k, string root)
    {
        // k.name
        var members = AllMethodsInClass(k).ToArray();

        NamedOrder? lastClass = null;
        memberdefType? lastMember = null;

        foreach (var member in members.OrderBy(x => x.Location.line))
        {
            var newClass = Classify(k, member);
            if (lastClass != null)
            {
                if (member.Location.file != lastMember!.Location.file)
                {
                    throw new Exception("invalid data");
                }

                if (lastClass.Order > newClass.Order)
                {
                    string primaryFile = DoxygenUtils.LocationToString(member.Location, root);
                    string secondaryFile = DoxygenUtils.LocationToString(lastMember.Location, root);
                    string errorMessage = $"Members for {k.name} are orderd badly, can't go from {lastClass.Name} ({MemberToString(lastMember!)}) to {newClass.Name} ({MemberToString(member)})!";
                    return new CheckError(k, primaryFile, secondaryFile, errorMessage);
                }
            }
            lastClass = newClass;
            lastMember = member;
        }

        return null;
    }

    private static void PrintError(Printer printer, CompoundType k, string primaryFile, string secondaryFile, string errorMessage)
    {
        var members = AllMethodsInClass(k).ToArray();
        printer.Error(primaryFile, errorMessage);
        AnsiConsole.WriteLine($"{secondaryFile}: From here");

        AnsiConsole.WriteLine("Something better could be:");
        AnsiConsole.WriteLine("");

        // print better order
        PrintSuggestedOrder(k, members);
        AnsiConsole.WriteLine("");
        AnsiConsole.WriteLine("");
    }

    private static void PrintSuggestedOrder(CompoundType k, memberdefType[] members)
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
                AnsiConsole.MarkupLineInterpolated($"  [green]{MemberToString(it)}[/]");
            }
            AnsiConsole.WriteLine("");
        }
    }

    private static string MemberToString(memberdefType it)
    {
        if (it.Kind == DoxMemberKind.Function)
        {
            return $"{it.Type} {it.Name}{it.Argsstring}";
        }
        return $"{it.Type} {it.Name}";
    }

    private static IEnumerable<memberdefType> AllMethodsInClass(CompoundType k)
    {
        return k.Compund.Compound.Sectiondef.SelectMany(x => x.memberdef);
    }

    private record NamedOrder(string Name, int Order)
    {
        internal NamedOrder Specify(string visibility, int orderChange)
        {
            return new NamedOrder($"{visibility} {Name}", Order + orderChange);
        }
    }

    private static NamedOrder typedefs = new NamedOrder("typedefs", -40);
    private static NamedOrder friends = new NamedOrder("friends", -30);
    private static NamedOrder enums = new NamedOrder("enums", -20);
    private static NamedOrder vars = new NamedOrder("variables", -10);
    private static NamedOrder constructors = new NamedOrder("constructors", 0);
    private static NamedOrder defaults = new NamedOrder("defaults", 1);
    private static NamedOrder deleted = new NamedOrder("deleted", 2);
    private static NamedOrder creators = new NamedOrder("creators", 5);
    private static NamedOrder manipulator = new NamedOrder("manipulators", 10);
    private static NamedOrder calculators = new NamedOrder("calculators", 15);
    private static NamedOrder accessor = new NamedOrder("acessors", 20);
    private static NamedOrder operators = new NamedOrder("operators", 30);
    private static NamedOrder utils = new NamedOrder("utils", 35);
    private static NamedOrder overrides = new NamedOrder("override", 37);
    private static NamedOrder virtuals = new NamedOrder("virtual", 40);
    private static NamedOrder pureVirtuals = new NamedOrder("pure virtual", 45);

    private static NamedOrder Classify(CompoundType k, memberdefType m)
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

    private static NamedOrder SubClassify(CompoundType k, memberdefType m)
    {
        if(m.Argsstring?.EndsWith("=delete") ?? false)
        {
            return deleted;
        }

        if (m.Argsstring?.EndsWith("=default") ?? false)
        {
            if(DoxygenUtils.IsConstructorOrDestructor(m) && m.Param.Length == 0)
            {
                // empty constructors and destrctors are not-default even when they are defaulted
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

        if(m.Virt != null && m.Virt != DoxVirtualKind.NonVirtual)
        {
            if (DoxygenUtils.IsFunctionOverride(m))
            {
                return overrides;
            }

            if (m.Virt == DoxVirtualKind.Virtual && DoxygenUtils.IsConstructorOrDestructor(m) == false)
            {
                return virtuals;
            }

            if (m.Virt == DoxVirtualKind.PureVirtual)
            {
                return pureVirtuals;
            }
        }

        if (m.Kind == DoxMemberKind.Function)
        {
            if (DoxygenUtils.IsFunctionOverride(m))
            {
                return overrides;
            }

            if (m.Static == DoxBool.Yes)
            {
                // undecorated return AND that is a ref AND that references the current class
                if (m.Type?.Nodes.Any(node => node is Ref ret && ret.Value.refid == k.refid) ?? false)
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
            else if (m.Const == DoxBool.Yes || m.Constexpr == DoxBool.Yes)
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

        if(m.Kind == DoxMemberKind.Typedef)
        {
            return typedefs;
        }

        if (m.Kind == DoxMemberKind.Enum)
        {
            return enums;
        }

        if(m.Kind == DoxMemberKind.Friend)
        {
            return friends;
        }

        return new NamedOrder("everything", 0);

        
    }
}
