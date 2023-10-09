using Workbench.CMake;

namespace Workbench;

internal static class Status
{
    private static void PrintFoundList(Printer printer, string name, List<Found> list)
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
        PrintFoundList(printer, "cmake", CmakeTools.ListAllInstallations(printer).ToList());

        var root = Environment.CurrentDirectory;
        printer.Info($"Root: {root}");

        var projectBuildFolder = CompileCommands.F.FindBuildRootOrNull(root);
        if (projectBuildFolder == null)
        {
            printer.Error("Unable to find build folder");
        }
        else
        {
            printer.Info($"Project build folder: {projectBuildFolder}");
        }

        var ccs = cc.GetPathToCompileCommandsOrNull(printer);
        if (ccs != null)
        {
            printer.Info($"Compile commands: {ccs}");
        }
    }
}
