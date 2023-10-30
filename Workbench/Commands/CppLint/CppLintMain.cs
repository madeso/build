using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;
using Workbench.Shared;

namespace Workbench.Commands.CppLint;



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

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        return Log.PrintErrorsAtExit(log =>
        {
            var root = Cli.RequireDirectory(log, arg.Root, "ls directory");
            if (root == null)
            {
                return -1;
            }

            return Log.PrintErrorsAtExit(print => Cpplint.HandleList(print, root));
        });
    }
}

[Description("run all files")]
internal sealed class RunCommand : AsyncCommand<RunCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Root folder (if different from cwd)")]
        [CommandOption("--root")]
        [DefaultValue(null)]
        public string? Root { get; set; } = null;
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        return await Log.PrintErrorsAtExitAsync(async log =>
        {
            var root = Cli.RequireDirectory(log, arg.Root, "root");
            if (root == null)
            {
                return -1;
            }
            return await Log.PrintErrorsAtExitAsync(async print => await Cpplint.HandleRun(print, root));
        });
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

