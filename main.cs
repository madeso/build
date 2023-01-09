using Spectre.Console.Cli;

namespace Workbench
{
    internal class Program
    {
        static int Main(string[] args)
        {
            var app = new CommandApp();
            app.Configure( config => {
                #if DEBUG
                    config.PropagateExceptions();
                    config.ValidateExamples();
                #endif
                config.AddCommand<IndentationCommand>("size");
                Cmake.Main.Configure(config, "cmake");
                Git.Main.Configure(config, "git");
                CompileCommands.Main.Configure(config, "compile-commands");
                CheckIncludes.Main.Configure(config, "check-includes");
            });
            return app.Run(args);
        }
    }
}
