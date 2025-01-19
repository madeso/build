using Spectre.Console;
using Spectre.Console.Rendering;
using Workbench.Shared;
using Workbench.Shared.CMake;

namespace Workbench.Commands.Status;

internal static class Status
{
    internal static void HandleStatus(Vfs vfs, Dir cwd, Log log, CompileCommandsArguments cc, Config.Paths paths)
    {
        AnsiConsole.WriteLine($"Root: {cwd}");

        FindCMake.FindAllInstallations().PrintFoundList("cmake", FindCMake.FindInstallationOrNull());
        FindCMake.ListAllBuilds(cwd, cc).PrintFoundList("cmake build", FindCMake.FindBuildOrNone(cwd, cc, null));
        CompileCommand.ListAll(vfs, cwd, cc, paths)
            .PrintFoundList("compile command", CompileCommand.FindOrNone(vfs, cwd, cc, null, paths));
    }
}
