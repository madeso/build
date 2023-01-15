using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text.Json.Serialization;

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

    public IEnumerable<string> GetRelativeIncludes()
    {
        // shitty comamndline parser... beware
        foreach (var c in command.Split(' '))
        {
            const string include_prefix = "-I";
            if (c.StartsWith(include_prefix) == false) { continue; }

            yield return c[include_prefix.Length..].Trim();
        }
    }

    public Dictionary<string, string> GetDefines()
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
    [JsonPropertyName("file")]
    public string file = "";

    [JsonPropertyName("directory")]
    public string directory = "";

    [JsonPropertyName("command")]
    public string command = "";
}

internal static class Utils
{
    internal static Dictionary<string, CompileCommand>? LoadCompileCommandsOrNull(Printer printer, string path)
    {
        var content = File.ReadAllText(path);
        var store = JsonUtil.Parse<List<CompileCommandJson>>(printer, path, content);

        if (store == null)
        {
            printer.Error($"Unable to load compile commands from {path}");
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
    public static string? FindBuildRootOrNull(string root)
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


internal class CommonArguments : CommandSettings
{
    [Description("the path to compile_commands.json")]
    [CommandOption("--compile-commands")]
    [DefaultValue(null)]
    private string? compileCommands { get; set; }

    public string? GetPathToCompileCommandsOrNull(Printer print)
    {
        var ret = get_argument_or_none(Environment.CurrentDirectory);
        if (ret == null)
        {
            print.Error($"Unable to locate {Utils.COMPILE_COMMANDS_FILE_NAME}");
        }
        return ret;
    }

    private string? get_argument_or_none(string cwd)
    {
        if (compileCommands != null)
        {
            return compileCommands;
        }

        var r = Utils.FindBuildRootOrNull(cwd);
        if (r == null) { return null; }
        return Path.Join(r, Utils.COMPILE_COMMANDS_FILE_NAME);
    }
}



