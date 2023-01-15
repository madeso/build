using Workbench.CMake;

namespace Workbench;

internal static class Status
{
    private static void print_found_list(Printer printer, string name, List<Found> list)
    {
        var found = Found.GetFirstValueOrNull(list) ?? "<None>";
        printer.Info($"{name}: {found}");
        foreach (var f in list)
        {
            printer.Info($"    {f}");
        }
    }

    internal static void HandleStatus(Printer printer, CompileCommands.CommonArguments cc)
    {
        print_found_list(printer, "cmake", CmakeTools.ListAll(printer).ToList());

        var root = Environment.CurrentDirectory;
        printer.Info($"Root: {root}");

        var project_build_folder = CompileCommands.Utils.FindBuildRootOrNull(root);
        if (project_build_folder == null)
        {
            printer.Error("Unable to find build folder");
        }
        else
        {
            printer.Info($"Project build folder: {project_build_folder}");
        }

        var ccs = cc.GetPathToCompileCommandsOrNull(printer);
        if (ccs != null)
        {
            printer.Info($"Compile commands: {ccs}");
        }
    }
}
