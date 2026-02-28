using Spectre.Console.Cli;
using Commands = Workbench.Commands;

var app = new CommandApp();
app.Configure(config =>
{
#if DEBUG
    config.PropagateExceptions();
    config.ValidateExamples();
#endif
    config.UseStrictParsing();

    Commands.MinorCommands.ConfigureRemoveEmoji(config, "remove-emoji");
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
    Commands.Headers.Main.Configure(config, "headers");
    Commands.Clang.Main.Configure(config, "clang");
    Commands.Hero.Main.Configure(config, "hero");
    Commands.CppLint.Main.Configure(config, "cpplint");

    Commands.CheckForMissingPragmaOnce.Main.Configure(config, "check-missing-pragma-once");
    Commands.CheckForNoProjectFolders.Main.Configure(config, "check-no-project-folders");
    Commands.CheckFileNames.Main.Configure(config, "check-file-names");
    Commands.CheckOrderInFile.Main.Configure(config, "order-in-file");
    Commands.CheckNames.Main.Configure(config, "check-names");

    Commands.LineCount.Main.Configure(config, "line-count");
    Commands.Cloc.Main.Configure(config, "cloc");

    config.AddBranch("vs", c =>
    {
        c.SetDescription("Visual Studio specific commands");
        Commands.CleanupOutput.Main.Configure(c, "cleanup");
        Commands.SlnDeps.Main.Configure(c, "slndeps");
    });

    Commands.CodeCity.Main.Configure(config, "code-city");
    Commands.Todo.Main.Configure(config, "todo");

    Commands.Dependencies.Main.Configure(config, "deps");
    Commands.FileDependencies.Main.Configure(config, "file-deps");

    Commands.CodeHistory.Main.Configure(config, "code-history");

    Commands.Paths.Main.Configure(config, "path");

    Commands.Folder.Main.Configure(config, "folder");

    Commands.Badge.Main.Configure(config, "badge");

    Commands.ExtractWordcloud.Main.Configure(config, "extract-wordcloud");
});
return app.Run(args);
