using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;
using Workbench.Shared;
using Workbench.Shared.CMake;

namespace Workbench.Commands.Cmake;


internal sealed class TraceCommand : AsyncCommand<TraceCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Directory to trace")]
        [CommandArgument(0, "<input directory>")]
        public string Directory { get; set; } = "";
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var cwd = Dir.CurrentDirectory;
        var vfs = new VfsDisk();

        return await CliUtil.PrintErrorsAtExitAsync(async printer => {
            var cmake = FindCMake.RequireInstallationOrNull(vfs, printer);

            if (cmake == null)
            {
                printer.Error("Failed to find cmake");
                return -1;
            }

            AnsiConsole.MarkupLineInterpolated($"Loading [green]{settings.Directory}[/].");

            try
            {
                var lines = await CMakeTrace.TraceDirectoryAsync(cwd, cmake, new Dir(settings.Directory));
                foreach (var li in lines)
                {
                    AnsiConsole.MarkupLineInterpolated($"Running [green]{li.Cmd}[/].");
                }
            }
            catch (CMakeTrace.TraceError x)
            {
                AnsiConsole.MarkupLineInterpolated($"Error: [red]{x.Message}[/]");
            }

            return 0;
        });
    }
}

internal sealed class DotCommand : AsyncCommand<DotCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("CMake root")]
        [CommandArgument(0, "<cmake root>")]
        public string Directory { get; set; } = "";

        [Description("Simplify dotfile output")]
        [CommandOption("--simplify")]
        [DefaultValue(false)]
        public bool Simplify { get; set; }

        [Description("Remove empty projects from dotfile")]
        [CommandOption("--remove-empty")]
        [DefaultValue(false)]
        public bool RemoveEmpty { get; set; }

        [Description("Remove interface libraries")]
        [CommandOption("--remove-interfaces")]
        [DefaultValue(false)]
        public bool RemoveInterface { get; set; }

        [Description("Reverse arrows")]
        [CommandOption("--reverse")]
        [DefaultValue(false)]
        public bool Reverse { get; set; }

        [Description("Where to write dot")]
        [CommandArgument(1, "[output file]")]
        [DefaultValue("output.dot")]
        public string Output { get; set; } = "";

        [Description("Project names to ignore")]
        [CommandOption("--ignores")]
        public string[] NamesToIgnore { get; set; } = Array.Empty<string>(); 
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var cwd = Dir.CurrentDirectory;
        var paths = new Config.RealPaths();
        var vfs = new VfsDisk();

        return await CliUtil.PrintErrorsAtExitAsync(async printer => {
            var cmake = FindCMake.RequireInstallationOrNull(vfs, printer);

            if(cmake == null)
            {
                printer.Error("Failed to find cmake");
                return -1;
            }

            var dir = Cli.RequireDirectory(vfs, cwd, printer, settings.Directory, "cmake root");
            if(dir == null)
            {
                printer.Error("Failed to find cmake root");
                return -1;
            }

            AnsiConsole.MarkupLineInterpolated($"Loading [green]{dir}[/].");

            try
            {
                var ignores = settings.NamesToIgnore.ToImmutableHashSet();
                AnsiConsole.MarkupLineInterpolated($"Ignoring [red]{ignores.Count}[/] projects.");
                var lines = await CMakeTrace.TraceDirectoryAsync(cwd, cmake, dir);
                var solution = Solution.Parse.CMake(lines);

                if (settings.RemoveInterface)
                {
                    var removed = solution.RemoveProjects(p => p.Type == Solution.ProjectType.Interface);
                    AnsiConsole.MarkupLineInterpolated($"Removing [red]{removed}[/] interfaces.");
                }

                solution.RemoveProjects(p => ignores.Contains(p.Name));
                if(settings.RemoveEmpty)
                {
                    solution.RemoveProjects(p => p.Uses.Any() == false && p.IsUsedBy.Any() == false);
                }

                var gv = solution.MakeGraphviz(settings.Reverse);

                if (settings.Simplify)
                {
                    gv.Simplify();
                }

                await gv.SmartWriteFileAsync(vfs, paths, cwd, Cli.ToSingleFile(cwd, settings.Output, "cmake.dot"), printer);
            }
            catch (CMakeTrace.TraceError x)
            {
                AnsiConsole.MarkupLineInterpolated($"Error: [red]{x.Message}[/]");
            }

            return 0;
        });
    }
}

internal class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, cmake =>
        {
            cmake.AddCommand<TraceCommand>("trace");
            cmake.AddCommand<DotCommand>("dot");
        });
    }
}
