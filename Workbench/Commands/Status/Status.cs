using Spectre.Console;
using Spectre.Console.Rendering;
using Workbench.Shared;
using Workbench.Shared.CMake;

namespace Workbench.Commands.Status;

internal static class Status
{
    internal static void HandleStatus(VfsRead vread, Dir cwd, Log log, CompileCommandsArguments cc, Config.Paths paths)
    {
        AnsiConsole.WriteLine($"Root: {cwd}");

        FindCMake.FindAllInstallations().PrintFoundList("cmake", FindCMake.FindInstallationOrNull());
        FindCMake.ListAllBuilds(cwd, cc).PrintFoundList("cmake build", FindCMake.FindBuildOrNone(cwd, cc, null));
        CompileCommand.ListAll(vread, cwd, cc, paths)
            .PrintFoundList("compile command", CompileCommand.FindOrNone(vread, cwd, cc, null, paths));
    }
}
