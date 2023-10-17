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
            Commands.Build.Main.Configure(config, "build");
            Commands.Indent.Main.Configure(config, "indent");
            Commands.Cmake.Main.Configure(config, "cmake");
            Commands.Git.Main.Configure(config, "git");
            Commands.CompileCommands.Main.Configure(config, "compile-commands");
            Commands.CheckIncludeOrder.Main.Configure(config, "check-includes");
            Commands.ListHeaders.Main.Configure(config, "list-headers");
            Commands.Clang.Main.Configure(config, "clang");
            Commands.Hero.Main.Configure(config, "hero");
            Commands.CppLint.Main.Configure(config, "cpplint");
            Commands.Tools.Main.Configure(config, "tools");
            Commands.SlnDeps.Main.Configure(config, "slndeps");
            Commands.CodeCity.Main.Configure(config, "code-city");
            Commands.CheckOrderInFile.Main.Configure(config, "order-in-file");
            Commands.CheckNames.Main.Configure(config, "check-names");
            Commands.Todo.Main.Configure(config, "todo");
            Commands.Dependencies.Main.Configure(config, "deps");
        });
        return app.Run(args);
    }
}
