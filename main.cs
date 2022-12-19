using System;
using System.Diagnostics.CodeAnalysis;
using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Collections.Immutable;
using System.Security.Cryptography.X509Certificates;

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
            });
            return app.Run(args);
        }
    }
}
