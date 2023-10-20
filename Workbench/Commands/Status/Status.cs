using Spectre.Console;
using Spectre.Console.Rendering;
using Workbench.Shared;
using Workbench.Shared.CMake;

namespace Workbench.Commands.Status;

internal static class Status
{
    internal static void HandleStatus(Log log, CompileCommandsArguments cc)
    {
        var root = Environment.CurrentDirectory;
        AnsiConsole.WriteLine($"Root: {root}");

        print_found_list("cmake", FindCMake.FindInstallationOrNull(),
            FindCMake.FindAllInstallations());
        print_found_list("cmake build", FindCMake.FindBuildOrNone(cc, null),
            FindCMake.ListAllBuilds(cc));
        print_found_list("compile command", CompileCommand.FindOrNone(cc, null),
            CompileCommand.ListAll(cc));

        static void print_found_list<T>(string name, T? selected, IEnumerable<Found<T>> list)
        {
            var found = selected?.ToString() ?? "<None>";
            var table = new Table();

            // name/found is not exactly column title but works for now
            table.AddColumn(new TableColumn(name).RightAligned());
            table.AddColumn(new TableColumn(found).NoWrap());
            foreach (var f in list)
            {
                var renderables = f.Findings.Select<FoundEntry<T>, IRenderable>(f => f switch
                {
                    FoundEntry<T>.Result r => Markup.FromInterpolated($"[blue]{r.Value}[/]"),
                    FoundEntry<T>.Error e => Markup.FromInterpolated($"[red]{e.Reason}[/]"),
                    _ => throw new ArgumentOutOfRangeException(nameof(f), f, null)
                }).ToList();
                if (renderables.Count == 0)
                {
                    // spectre rows doesn't like empty container so hack around with a empty text
                    renderables.Add(new Text(string.Empty));
                }
                var rows = new Rows(renderables);
                table.AddRow(new Text(f.Name), rows);
            }

            table.Expand();
            AnsiConsole.Write(table);
        }
    }
}
