using Workbench.CMake;

namespace Workbench;

internal static class Status
{
    private static void PrintFoundList(Printer printer, string name, List<Found> list)
    {
        var found = Found.GetFirstValueOrNull(list) ?? "<None>";
        Printer.Info($"{name}: {found}");
        foreach (var f in list)
        {
            Printer.Info($"    {f}");
        }
    }

    internal static void HandleStatus(Printer printer, CompileCommandsArguments cc)
    {
        PrintFoundList(printer, "cmake", CmakeTools.ListAllInstallations(printer).ToList());

        var root = Environment.CurrentDirectory;
        Printer.Info($"Root: {root}");

        var project_build_folder = CompileCommand.FindBuildRootOrNull(root);
        if (project_build_folder == null)
        {
            printer.Error("Unable to find build folder");
        }
        else
        {
            Printer.Info($"Project build folder: {project_build_folder}");
        }

        var ccs = cc.GetPathToCompileCommandsOrNull(printer);
        if (ccs != null)
        {
            Printer.Info($"Compile commands: {ccs}");
        }
    }
}
