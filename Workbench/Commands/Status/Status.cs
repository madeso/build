using Spectre.Console;
using Workbench.Shared;
using Workbench.Shared.CMake;

namespace Workbench.Commands.Status;

internal static class Status
{
    internal static void HandleStatus(Printer printer, CompileCommandsArguments cc)
    {
        print_found_list("cmake", FindCMake.FindAllInstallations(printer).ToList());

        var root = Environment.CurrentDirectory;
        AnsiConsole.WriteLine($"Root: {root}");

        var project_build_folder = CompileCommand.FindBuildRootOrNull(root);
        if (project_build_folder == null)
        {
            printer.Error("Unable to find build folder");
        }
        else
        {
            AnsiConsole.WriteLine($"Project build folder: {project_build_folder}");
        }

        var ccs = cc.GetPathToCompileCommandsOrNull(printer);
        if (ccs != null)
        {
            AnsiConsole.WriteLine($"Compile commands: {ccs}");
        }

        static void print_found_list(string name, List<Found> list)
        {
            var found = list.GetFirstValueOrNull() ?? "<None>";
            AnsiConsole.WriteLine($"{name}: {found}");
            foreach (var f in list)
            {
                AnsiConsole.WriteLine($"    {f}");
            }
        }
    }
}
