using Spectre.Console;
using Spectre.Console.Rendering;
using Workbench.Shared;
using Workbench.Shared.CMake;

namespace Workbench.Commands.Status;

internal static class Status
{
    internal static void HandleStatus(Dir cwd, Log log, CompileCommandsArguments cc)
    {
        AnsiConsole.WriteLine($"Root: {cwd}");

        FindCMake.FindAllInstallations().PrintFoundList("cmake", FindCMake.FindInstallationOrNull());
        FindCMake.ListAllBuilds(cwd, cc).PrintFoundList("cmake build", FindCMake.FindBuildOrNone(cwd, cc, null));
        CompileCommand.ListAll(cwd, cc).PrintFoundList("compile command", CompileCommand.FindOrNone(cwd, cc, null));
    }
}
