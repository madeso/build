using Spectre.Console;
using Workbench.CompileCommands;
using Workbench.Doxygen.Compound;
using Workbench.Doxygen.Index;
using static Workbench.Doxygen.Compound.linkedTextType;

namespace Workbench;

internal static class OrderInFile
{
    public static int Run(Printer printer, string file)
    {
        var parsed = Doxygen.Doxygen.ParseIndex(file);

        int checks = 0;
        int fails = 0;

        foreach(var k in parsed.compounds.Where(x => x.kind == Doxygen.Index.CompoundKind.Struct || x.kind == Doxygen.Index.CompoundKind.Class))
        {
            checks += 1;
            if (false == CheckClass(printer, k))
            {
                fails += 1;
            }
        }

        AnsiConsole.MarkupLineInterpolated($"[blue]{checks-fails} / {checks}[/] classes were accepted");

        if(checks > 0 && fails==0) { return 0; }
        else { return -1; }
    }

    private static bool CheckClass(Printer printer, CompoundType k)
    {
        // k.name
        var members = k.Compund.Compound.Sectiondef.SelectMany(x => x.memberdef).ToArray();

        NamedOrder? lastClass = null;
        Doxygen.Compound.memberdefType? lastMember = null;

        foreach (var member in members.OrderBy(x => x.Location.line))
        {
            var newClass = Classify(k, member);
            if (lastClass != null)
            {
                if(member.Location.file != lastMember!.Location.file)
                {
                    throw new Exception("invalid data");
                }

                if (lastClass.Order > newClass.Order)
                {
                    printer.Error($"Members for {k.name} are orderd badly, can't go from {lastClass.Name} ({MemberToString(lastMember!)}) to {newClass.Name} ({MemberToString(member)})!");

                    AnsiConsole.WriteLine("Something better could be:");
                    AnsiConsole.WriteLine("");

                    // print better order
                    var sorted = members
                        .GroupBy(x => Classify(k, x), (group, items) => (group, items.ToArray()))
                        .OrderBy(x => x.group.Order)
                        ;
                    foreach(var (c, items) in sorted)
                    {
                        AnsiConsole.MarkupLineInterpolated($"// [blue]{c.Name}[/]");
                        foreach(var it in items)
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

        static string MemberToString(memberdefType it)
        {
            if(it.Kind == DoxMemberKind.Function)
            {
                return $"{it.Type} {it.Name}{it.Argsstring}";
            }
            return $"{it.Type} {it.Name}";
        }
    }

    private record NamedOrder(string Name, int Order);

    private static NamedOrder typedefs = new NamedOrder("Typedefs", -40);
    private static NamedOrder friends = new NamedOrder("Friends", -30);
    private static NamedOrder enums = new NamedOrder("Enums", -20);
    private static NamedOrder publicvars = new NamedOrder("Public Variables", -10);
    private static NamedOrder constructors = new NamedOrder("Constructors", 0);
    private static NamedOrder creators = new NamedOrder("Creators", 5);
    private static NamedOrder manipulator = new NamedOrder("Manipulators", 10);
    private static NamedOrder accessor = new NamedOrder("Acessors", 20);
    private static NamedOrder operators = new NamedOrder("Operators", 30);
    private static NamedOrder utils = new NamedOrder("Utils", 35);
    private static NamedOrder PrivateVars = new NamedOrder("Private vars", 100);

    private static NamedOrder Classify(CompoundType k, memberdefType m)
    {
        if (m.Name.StartsWith("operator"))
        {
            return operators;
        }

        if (m.Kind == DoxMemberKind.Function)
        {
            if(m.Static == DoxBool.Yes)
            {
                // undecorated return AND that is a ref AND that references the current class
                if (m.Type?.Nodes.Length == 1 && m.Type?.Nodes[0] is Ref ret && ret.Value.refid == k.refid)
                {
                    return creators;
                }
                else
                {
                    return utils;
                }
            }
            else if(m.Const == DoxBool.Yes || m.Constexpr == DoxBool.Yes)
            {
                return accessor;
            }
            else if (m.Type?.Nodes.Length == 0)
            {
                // constructor
                return constructors;
            }
            else
            {
                return manipulator;
            }
        }

        if(m.Kind == DoxMemberKind.Variable)
        {
            if(m.Prot == DoxProtectionKind.Public) { return publicvars; }
            else { return PrivateVars; }
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
