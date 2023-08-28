using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

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

            Commands.StatusCommands.Main.ConfigureStatus(config, "status");
            Commands.BuildCommands.Main.Configure(config, "build");
            Commands.IndentCommands.Main.Configure(config, "indent");
            Commands.CmakeCommands.Main.Configure(config, "cmake");
            Commands.GitCommands.Main.Configure(config, "git");
            Commands.CompileCommandsCommands.Main.Configure(config, "compile-commands");
            Commands.CheckIncludesCommands.Main.Configure(config, "check-includes");
            Commands.ListHeadersCommands.Main.Configure(config, "list-headers");
            Commands.ClangCommands.Main.Configure(config, "clang");
            Commands.HeroCommands.Main.Configure(config, "hero");
            Commands.CppLintCommands.Main.Configure(config, "cpplint");
            Commands.ToolsCommands.Main.Configure(config, "tools");
            Commands.SlnDepsCommands.Main.Configure(config, "slndeps");
            Commands.OrderInFileCommands.Main.Configure(config, "order-in-file");
            Commands.CheckNamesCommands.Main.Configure(config, "check-names");
            Commands.TodoCommands.Main.Configure(config, "todo");
        });
        return app.Run(args);
    }
}
