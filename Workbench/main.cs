using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Workbench;

internal sealed class DummyCommand : Command<DummyCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Number")]
        [CommandOption("--number")]
        public int Number { get; set; } = 42;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print =>
        {
            print.cat($"Number is {settings.Number}");
            return 0;
        });
    }
}



internal class Program
{
    static int Main(string[] args)
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
#if DEBUG
            config.PropagateExceptions();
            config.ValidateExamples();
#endif

            config.AddCommand<DummyCommand>("dummy").WithDescription("Just a dummy");

            Commands.MinorCommands.ConfigureLs(config, "ls");
            Commands.MinorCommands.ConfigureCat(config, "cat");
            Commands.StatusCommands.Main.ConfigureStatus(config, "status");
            Commands.BuildCommands.Main.Configure(config, "build");
            Commands.IndentCommands.Main.Configure(config, "indent");
            Commands.CmakeCommands.Main.Configure(config, "cmake");
            Commands.GitCommands.Main.Configure(config, "git");

            Commands.CompileCommandsCommands.Main.Configure(config, "compile-commands");
            CheckIncludes.Main.Configure(config, "check-includes");
            ListHeaders.Main.Configure(config, "list-headers");
            Clang.Main.Configure(config, "clang");

            Hero.Main.Configure(config, "hero");
        });
        return app.Run(args);
    }
}
