﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;


namespace Workbench
{
    internal sealed class FileSizeCommand : Command<FileSizeCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [Description("Path to search. Defaults to current directory.")]
            [CommandArgument(0, "[searchPath]")]
            public string? SearchPath { get; init; }

            [CommandOption("-p|--pattern")]
            public string? SearchPattern { get; init; }

            [CommandOption("--hidden")]
            [DefaultValue(true)]
            public bool IncludeHidden { get; init; }
        }

        public override int Execute([NotNull] CommandContext context, [NotNull] Settings settings)
        {
            var searchOptions = new EnumerationOptions
            {
                AttributesToSkip = settings.IncludeHidden
                    ? FileAttributes.Hidden | FileAttributes.System
                    : FileAttributes.System
            };

            var searchPattern = settings.SearchPattern ?? "*.*";
            var searchPath = settings.SearchPath ?? Directory.GetCurrentDirectory();
            var files = new DirectoryInfo(searchPath)
                .GetFiles(searchPattern, searchOptions);

            var totalFileSize = files
                .Sum(fileInfo => fileInfo.Length);

            AnsiConsole.MarkupLine($"Total file size for [green]{searchPattern}[/] files in [green]{searchPath}[/]: [blue]{totalFileSize:N0}[/] bytes");

            return 0;
        }
    }

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
                config.AddCommand<FileSizeCommand>("size");
                CmakeMain.Configure(config, "cmake");
                GitMain.Configure(config, "git");
            });
            return app.Run(args);
        }
    }
}
