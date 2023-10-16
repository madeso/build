using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Workbench.Commands.CppLintCommands;



[Description("list all files")]
internal sealed class LsCommand : Command<LsCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Root folder (if different from cwd)")]
        [CommandOption("--root")]
        [DefaultValue(null)]
        public string? Root { get; set; } = null;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print => Cpplint.HandleList(print, settings.Root ?? Environment.CurrentDirectory));
    }
}

[Description("run all files")]
internal sealed class RunCommand : Command<RunCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Root folder (if different from cwd)")]
        [CommandOption("--root")]
        [DefaultValue(null)]
        public string? Root { get; set; } = null;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print => Cpplint.HandleRun(print, settings.Root ?? Environment.CurrentDirectory));
    }
}

internal class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, cmake =>
        {
            cmake.SetDescription("Runs cpplint on all sources");
            cmake.AddCommand<LsCommand>("ls");
            cmake.AddCommand<RunCommand>("run");
        });
    }
}

