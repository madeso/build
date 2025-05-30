using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;
using Workbench.Config;
using Workbench.Shared;
using Workbench.Shared.Extensions;

namespace Workbench.Commands.Dependencies;


internal sealed class ListGraphvizCommand : AsyncCommand<ListGraphvizCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Doxygen xml folder")]
        [CommandArgument(0, "[doxygen xml]")]
        public string DoxygenXml { get; init; } = string.Empty;

        [Description("Namespace filter")]
        [CommandArgument(1, "[namespace]")]
        public string NamespaceFilter { get; init; } = string.Empty;

        [Description("Output file")]
        [CommandArgument(2, "[output]")]
        public string OutputFile { get; init; } = string.Empty;

        [Description("Ignored classes")]
        [CommandOption("-x")]
        public string[] IgnoredClasses { get; init; } = Array.Empty<string>();

        [Description("Don't consider functions/methods when making connections")]
        [CommandOption("--no-functions")]
        public bool? NoIncludeFunctions { get; init; }

        [Description("Don't consider function arguments when making connections")]
        [CommandOption("--no-args")]
        public bool? NoAddArguments { get; init; }

        [Description("Don't consider any members when making connections")]
        [CommandOption("--no-members")]
        public bool? NoAddMembers { get; init; }

        [Description("Cluster on namespace")]
        [CommandOption("--cluster-namespace")]
        public bool? ClusterNamespace { get; init; }
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        var cwd = Dir.CurrentDirectory;
        var paths = new Config.RealPaths();
        var vfs = new VfsDisk();
        var exec = new SystemExecutor();

        return await CliUtil.PrintErrorsAtExitAsync(async printer =>
            {
                var dox = Cli.RequireDirectory(vfs, cwd, printer, arg.DoxygenXml, "doxygen xml folder");
                if (dox == null)
                {
                    return -1;
                }

                await Dependencies.WriteToGraphvizAsync(exec, vfs, paths, cwd, printer, dox,
                    arg.NamespaceFilter, new Fil(arg.OutputFile),
                    arg.IgnoredClasses.ToImmutableHashSet(),
                    !(arg.NoIncludeFunctions ?? false),
                    !(arg.NoAddArguments ?? false),
                    !(arg.NoAddMembers ?? false),
                    (arg.ClusterNamespace ?? false)
                );
                return 0;
            }
        );
    }
}


internal sealed class ListCallGraph : AsyncCommand<ListCallGraph.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Doxygen xml folder")]
        [CommandArgument(0, "[doxygen xml]")]
        public string DoxygenXml { get; init; } = string.Empty;

        [Description("Output file")]
        [CommandArgument(2, "[output]")]
        public string OutputFile { get; init; } = string.Empty;

        [Description("Cluster on ...?")]
        [CommandOption("--cluster-on")]
        public Dependencies.ClusterCallGraphOn? ClusterOn { get; init; }
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        var cwd = Dir.CurrentDirectory;
        var paths = new Config.RealPaths();
        var vfs = new VfsDisk();
        var exec = new SystemExecutor();

        return await CliUtil.PrintErrorsAtExitAsync(async log =>
        {
            var dox = Cli.RequireDirectory(vfs, cwd, log, arg.DoxygenXml, "Doxygen xml folder");
            if (dox == null)
            {
                return -1;
            }

            await Dependencies.WriteCallGraphToGraphvizAsync(exec, vfs, paths, cwd,
                log, dox, new Fil(arg.OutputFile),
                arg.ClusterOn ?? Dependencies.ClusterCallGraphOn.None);
            
            return 0;
        });
    }
}


internal sealed class PrintCommand : Command<PrintCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Doxygen xml folder")]
        [CommandArgument(0, "[doxygen xml]")]
        public string DoxygenXml { get; init; } = string.Empty;

        [Description("Namespace filter")]
        [CommandArgument(1, "[namespace]")]
        public string NamespaceFilter { get; init; } = string.Empty;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        var cwd = Dir.CurrentDirectory;
        var vfs = new VfsDisk();

        return CliUtil.PrintErrorsAtExit(printer =>
            {
                var dox = Cli.RequireDirectory(vfs, cwd, printer, arg.DoxygenXml, "Doxygen xml folder");
                if (dox == null)
                {
                    return -1;
                }

                Dependencies.PrintLists(printer, dox, arg.NamespaceFilter);
                return 0;
            }
        );
    }
}

public static class Main
{
    public static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, root =>
        {
            root.SetDescription("Dependency commands");

            root.AddCommand<ListGraphvizCommand>("list").WithDescription("Write dependencies to graphviz");
            root.AddCommand<ListCallGraph>("calls").WithDescription("Write call graph to graphviz");
            root.AddCommand<PrintCommand>("print").WithDescription("Print all classes and functions in a namespace");
        });
    }
}