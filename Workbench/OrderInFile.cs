using Spectre.Console;
using Workbench.Doxygen.Compound;
using Workbench.Doxygen.Index;

namespace Workbench;

internal static class OrderInFile
{
    public static int Run(Printer printer, string file)
    {
        var parsed = Doxygen.Doxygen.ParseIndex(file);

        bool ok = true;

        foreach(var k in parsed.compounds.Where(x => x.kind == Doxygen.Index.CompoundKind.Struct || x.kind == Doxygen.Index.CompoundKind.Class))
        {
            if(false == CheckClass(printer, k))
            {
                ok = false;
            }
        }

        if(ok) { return 0; }
        else { return -1; }
    }

    private static bool CheckClass(Printer printer, CompoundType k)
    {
        // k.name
        var members = k.Compund.Compound.Sectiondef.SelectMany(x => x.memberdef).ToArray();

        NamedOrder? lastClass = null;

        foreach (var member in members.OrderBy(x => x.Location.line))
        {
            var newClass = Classify(member);
            if (lastClass != null)
            {
                if (lastClass.Order > newClass.Order)
                {
                    printer.Error($"Members for {k.name} are orderd badly, can't go from {lastClass.Name} to {newClass.Name}!");

                    AnsiConsole.MarkupLineInterpolated($"Something [green]better[/] could be:");
                    AnsiConsole.WriteLine("");

                    // print better order
                    var sorted = members.GroupBy(Classify, (group, items) => (group, items.ToArray()));
                    foreach(var (c, items) in sorted)
                    {
                        AnsiConsole.MarkupLineInterpolated($"// [blue]{c.Name}[/]");
                        foreach(var it in items)
                        {
                            AnsiConsole.MarkupLineInterpolated($"  [green]{it.Name}[/]");
                        }
                        AnsiConsole.WriteLine("");
                    }
                    AnsiConsole.WriteLine("");
                    AnsiConsole.WriteLine("");

                    return false;
                }
            }
            lastClass = newClass;
        }

        return true;
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
