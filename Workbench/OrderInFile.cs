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

        foreach (var k in AllClasses(parsed))
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
        int fails = 0;

        foreach (var k in AllClasses(parsed))
        {
            checks += 1;
            if (false == CheckClass(printer, k, root))
            {
                fails += 1;
            }
        }

        AnsiConsole.MarkupLineInterpolated($"[blue]{checks - fails} / {checks}[/] classes were accepted");

        if (checks > 0 && fails == 0) { return 0; }
        else { return -1; }
    }

    private static IEnumerable<CompoundType> AllClasses(Doxygen.Index.DoxygenType parsed)
    {
        return parsed.compounds.Where(x => x.kind == Doxygen.Index.CompoundKind.Struct || x.kind == Doxygen.Index.CompoundKind.Class);
    }

    private static bool CheckClass(Printer printer, CompoundType k, string root)
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
                    printer.Error(LocationToString(member.Location, root), $"Members for {k.name} are orderd badly, can't go from {lastClass.Name} ({MemberToString(lastMember!)}) to {newClass.Name} ({MemberToString(member)})!");
                    AnsiConsole.WriteLine($"{LocationToString(lastMember.Location, root)}: From here");

                    AnsiConsole.WriteLine("Something better could be:");
                    AnsiConsole.WriteLine("");

                    // print better order
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
                    AnsiConsole.WriteLine("");
                    AnsiConsole.WriteLine("");

                    return false;
                }
            }
            lastClass = newClass;
            lastMember = member;
        }

        return true;

        static string LocationToString(locationType loc, string root)
        {
            var abs = new FileInfo(Path.Join(root, loc.file)).FullName;
            var print = File.Exists(abs) ? abs : loc.file;
            return $"{print}({loc.line})";
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
    private static NamedOrder creators = new NamedOrder("creators", 5);
    private static NamedOrder manipulator = new NamedOrder("manipulators", 10);
    private static NamedOrder calculators = new NamedOrder("calculators", 15);
    private static NamedOrder accessor = new NamedOrder("acessors", 20);
    private static NamedOrder operators = new NamedOrder("operators", 30);
    private static NamedOrder utils = new NamedOrder("utils", 35);
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
        if (m.Name.StartsWith("operator"))
        {
            return operators;
        }

        if(m.Virt == DoxVirtualKind.Virtual && IsConstructorOrDestructor(m) == false)
        {
            return virtuals;
        }

        if (m.Virt == DoxVirtualKind.PureVirtual)
        {
            return pureVirtuals;
        }

        if (m.Kind == DoxMemberKind.Function)
        {
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
            else if (IsConstructorOrDestructor(m))
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

        static bool IsConstructorOrDestructor(memberdefType m)
        {
            var ret = m.Type?.Nodes;
            if (ret == null) { return true; }

            // exclude "constexpr" return values
            var rets = ret.Where(node => !(node is linkedTextType.Text text && IsKeyword(text))).ToArray();
            var retCount = rets.Count();

            // Console.WriteLine($"ret detection: {m.Name} -- {retCount}: {ret}");
            return retCount == 0;

            static bool IsKeyword(linkedTextType.Text text)
            {
                return text.Value.Trim() switch
                {
                    "constexpr" => true,
                    "const" => true,
                    "&" => true,
                    _ => false,
                };
            }
        }
    }
}
