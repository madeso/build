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

        FindCMake.FindAllInstallations().PrintFoundList("cmake", FindCMake.FindInstallationOrNull());
        FindCMake.ListAllBuilds(cc).PrintFoundList("cmake build", FindCMake.FindBuildOrNone(cc, null));
        CompileCommand.ListAll(cc).PrintFoundList("compile command", CompileCommand.FindOrNone(cc, null));
    }
}
