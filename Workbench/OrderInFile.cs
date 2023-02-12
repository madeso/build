using Spectre.Console;
using Workbench.Doxygen.Compound;
using Workbench.Doxygen.Index;

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
            var newClass = Classify(member);
            if (lastClass != null)
            {
                if (lastClass.Order > newClass.Order)
                {
                    printer.Error($"Members for {k.name} are orderd badly, can't go from {lastClass.Name} ({MemberToString(lastMember!)}) to {newClass.Name} ({MemberToString(member)})!");

                    AnsiConsole.WriteLine("Something better could be:");
                    AnsiConsole.WriteLine("");

                    // print better order
                    var sorted = members.GroupBy(Classify, (group, items) => (group, items.ToArray()));
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
            return it.Name;
        }
    }

    private record NamedOrder(string Name, int Order);

    private static NamedOrder typedefs = new NamedOrder("Typedefs", -4);
    private static NamedOrder friends = new NamedOrder("Friends", -3);
    private static NamedOrder enums = new NamedOrder("Enums", -2);
    private static NamedOrder publicvars = new NamedOrder("Public Variables", -1);
    private static NamedOrder creator = new NamedOrder("Creators", 0);
    private static NamedOrder manipulator = new NamedOrder("Manipulators", 1);
    private static NamedOrder accessor = new NamedOrder("Acessors", 2);
    private static NamedOrder operators = new NamedOrder("Operators", 3);

    private static NamedOrder PrivateVars = new NamedOrder("Private vars", 100);

    private static NamedOrder Classify(memberdefType m)
    {
        if (m.Name.StartsWith("operator"))
        {
            return operators;
        }

        if (m.Kind == DoxMemberKind.Function)
        {
            if(m.Const == DoxBool.Yes || m.Constexpr == DoxBool.Yes)
            {
                return accessor;
            }
            else if (m.Type.Nodes.Length == 0)
            {
                // constructor
                return creator;
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
