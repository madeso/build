using Spectre.Console.Cli;

namespace Workbench;

internal class Program
{
    private static int Main(string[] args)
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
#if DEBUG
            config.PropagateExceptions();
            config.ValidateExamples();
#endif

            Commands.MinorCommands.ConfigureLs(config, "ls");
            Commands.MinorCommands.ConfigureCat(config, "cat");
            Commands.MinorCommands.ConfigureCatDir(config, "cat-dir");

            Commands.Status.Main.ConfigureStatus(config, "status");
            Commands.BuildCommands.Main.Configure(config, "build");
            Commands.Indent.Main.Configure(config, "indent");
            Commands.CmakeCommands.Main.Configure(config, "cmake");
            Commands.GitCommands.Main.Configure(config, "git");
            Commands.CompileCommandsCommands.Main.Configure(config, "compile-commands");
            Commands.CheckIncludes.Main.Configure(config, "check-includes");
            Commands.ListHeaders.Main.Configure(config, "list-headers");
            Commands.Clang.Main.Configure(config, "clang");
            Commands.HeroCommands.Main.Configure(config, "hero");
            Commands.CppLint.Main.Configure(config, "cpplint");
            Commands.Tools.Main.Configure(config, "tools");
            Commands.SlnDeps.Main.Configure(config, "slndeps");
            Commands.OrderInFile.Main.Configure(config, "order-in-file");
            Commands.CheckNames.Main.Configure(config, "check-names");
            Commands.Todo.Main.Configure(config, "todo");
            Commands.Dependencies.Main.Configure(config, "deps");
        });
        return app.Run(args);
    }
}
