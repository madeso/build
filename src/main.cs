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

                MinorCommands.Main.ConfigureLs(config, "ls");
                MinorCommands.Main.ConfigureCat(config, "cat");
                MinorCommands.Main.ConfigureDebug(config, "status");

                Build.Main.Configure(config, "build");
                Indent.Main.Configure(config, "indent");
                Cmake.Main.Configure(config, "cmake");
                Git.Main.Configure(config, "git");
                CompileCommands.Main.Configure(config, "compile-commands");
                CheckIncludes.Main.Configure(config, "check-includes");
                ListHeaders.Main.Configure(config, "list-headers");
                Clang.Main.Configure(config, "clang");

                Hero.Main.Configure(config, "hero");
            });
            return app.Run(args);
        }
    }
}
