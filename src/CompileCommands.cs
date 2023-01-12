using Newtonsoft.Json;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Workbench.CompileCommands;

public class CompileCommand
{
    public string directory;
    public string command;

    public CompileCommand(string directory, string command)
    {
        this.directory = directory;
        this.command = command;
    }

    public IEnumerable<string> get_relative_includes()
    {
        // shitty comamndline parser... beware
        foreach (var c in command.Split(' '))
        {
            const string include_prefix = "-I";
            if (c.StartsWith(include_prefix) == false) { continue; }

            yield return c[include_prefix.Length..].Trim();
        }
    }

    public Dictionary<string, string> get_defines()
    {
        // shitty comamndline parser... beware
        var r = new Dictionary<string, string>();

        foreach (var c in command.Split(' '))
        {
            const string define_prefix = "-D";
            if (c.StartsWith(define_prefix) == false) { continue; }

            var def = c[define_prefix.Length..];

            var arr = def.Split('=', 2);
            var key = arr[0];
            var val = arr.Length == 1 ? "" : arr[1];
            r.Add(key, val);
        }

        return r;
    }
}



internal class CompileCommandJson
{
    [JsonProperty("file")]
    public string file = "";

    [JsonProperty("directory")]
    public string directory = "";

    [JsonProperty("command")]
    public string command = "";
}

internal static class Utils
{
    internal static Dictionary<string, CompileCommand>? load_compile_commands(Printer printer, string path)
    {
        var content = File.ReadAllText(path);
        var store = JsonUtil.Parse<List<CompileCommandJson>>(printer, path, content);

        if (store == null)
        {
            printer.error($"Unable to load compile commands from {path}");
            return null;
        }

        var r = new Dictionary<string, CompileCommand>();
        foreach (var entry in store)
        {
            r.Add
            (
                entry.file,
                new CompileCommand
                (
                    directory: entry.directory,
                    command: entry.command
                )
            );
        }

        return r;
    }

    internal const string COMPILE_COMMANDS_FILE_NAME = "compile_commands.json";

    /// find the build folder containing the compile_commands file or None
    public static string? find_build_root(string root)
    {
        var common_roots = new string[] { "build", "build/debug-clang" };

        foreach (var relative_build in common_roots)
        {
            var build = Path.Join(root, relative_build);
            var compile_commands_json = Path.Join(build, COMPILE_COMMANDS_FILE_NAME);
            if (Path.Exists(compile_commands_json))
            {
                return build;
            }
        }

        return null;
    }
}


internal class MainCommandSettings : CommandSettings
{
    [Description("the path to compile_commands.json")]
    [CommandOption("--compile-commands")]
    [DefaultValue(null)]
    string? compile_commands { get; set; }

    public string? get_argument_or_none_with_cwd()
    {
        return get_argument_or_none(Environment.CurrentDirectory);
    }

    public string? get_argument_or_none(string cwd)
    {
        if (compile_commands != null)
        {
            return compile_commands;
        }

        var r = Utils.find_build_root(cwd);
        if (r == null) { return null; }
        return Path.Join(r, Utils.COMPILE_COMMANDS_FILE_NAME);
    }
}


internal sealed class FilesCommand : Command<FilesCommand.Arg>
{
    public sealed class Arg : MainCommandSettings
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter
            (
                print =>
                {

                    var path = settings.get_argument_or_none_with_cwd();
                    if (path != null)
                    {
                        var commands = Utils.load_compile_commands(print, path);
                        if (commands == null) { return -1; }

                        print.Info($"{commands}");
                    }
                    return 0;
                }
            );
    }
}


internal sealed class IncludesCommand : Command<IncludesCommand.Arg>
{
    public sealed class Arg : MainCommandSettings
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter
            (
                print =>
                {
                    var path = settings.get_argument_or_none_with_cwd();
                    if (path != null)
                    {
                        var commands = Utils.load_compile_commands(print, path);
                        if (commands == null) { return -1; }

                        foreach (var (file, command) in commands)
                        {
                            print.Info($"{file}");
                            var dirs = command.get_relative_includes();
                            foreach (var d in dirs)
                            {
                                print.Info($"    {d}");
                            }
                        }
                    }
                    return 0;
                }
            );
    }
}

internal sealed class DefinesCommand : Command<DefinesCommand.Arg>
{
    public sealed class Arg : MainCommandSettings
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter
            (
                print =>
                {
                    var path = settings.get_argument_or_none_with_cwd();
                    if (path != null)
                    {
                        var commands = Utils.load_compile_commands(print, path);
                        if (commands == null) { return -1; }

                        foreach (var (file, command) in commands)
                        {
                            print.Info($"{file}");
                            var defs = command.get_defines();
                            foreach (var (k, v) in defs)
                            {
                                print.Info($"    {k} = {v}");
                            }
                        }
                    }
                    return 0;
                }
            );
    }
}

internal static class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, cmake =>
        {
            cmake.SetDescription("Tool to list headers");
            cmake.AddCommand<FilesCommand>("files").WithDescription("list all files in the compile commands class");
            cmake.AddCommand<IncludesCommand>("includes").WithDescription("list include directories per file");
            cmake.AddCommand<DefinesCommand>("defines").WithDescription("list include directories per file");
        });
    }
}




