using System.ComponentModel;
using System.Text.Json.Serialization;
using Spectre.Console.Cli;
using Workbench.Utils;

namespace Workbench;

public class CompileCommand
{
    public string Directory;
    public string Command;

    public CompileCommand(string directory, string command)
    {
        Directory = directory;
        Command = command;
    }

    public IEnumerable<string> GetRelativeIncludes()
    {
        // shitty commandline parser... beware
        foreach (var c in Command.Split(' '))
        {
            const string INCLUDE_PREFIX = "-I";
            if (c.StartsWith(INCLUDE_PREFIX) == false) { continue; }

            yield return c[INCLUDE_PREFIX.Length..].Trim();
        }
    }

    public Dictionary<string, string> GetDefines()
    {
        // shitty commandline parser... beware
        var r = new Dictionary<string, string>();

        foreach (var c in Command.Split(' '))
        {
            const string DEFINE_PREFIX = "-D";
            if (c.StartsWith(DEFINE_PREFIX) == false) { continue; }

            var def = c[DEFINE_PREFIX.Length..];

            var arr = def.Split('=', 2);
            var key = arr[0];
            var val = arr.Length == 1 ? "" : arr[1];
            r.Add(key, val);
        }

        return r;
    }

    internal class CompileCommandJson
    {
        [JsonPropertyName("file")]
        public string File = "";

        [JsonPropertyName("directory")]
        public string Directory = "";

        [JsonPropertyName("command")]
        public string Command = "";
    }

    internal static Dictionary<string, CompileCommand>? LoadCompileCommandsOrNull(Printer printer, string path)
    {
        var content = File.ReadAllText(path);
        var store = JsonUtil.Parse<List<CompileCommandJson>>(printer, path, content);

        if (store == null)
        {
            printer.Error($"Unable to load compile commands from {path}");
            return null;
        }

        return store.ToDictionary(entry => entry.File,
            entry => new CompileCommand(directory: entry.Directory, command: entry.Command)
        );
    }

    internal const string JSON_FILE_NAME = "compile_commands.json";

    /// find the build folder containing the compile_commands file or None
    public static string? FindBuildRootOrNull(string root)
        => FileUtil
            .PitchforkBuildFolders(root)
            .FirstOrDefault(build => Path.Exists(Path.Join(build, JSON_FILE_NAME)))
    ;
}




internal class CompileCommandsArguments : CommandSettings
{
    [Description("the path to compile_commands.json")]
    [CommandOption("--compile-commands")]
    [DefaultValue(null)]
    public string? CompileCommands { get; set; }

    public string? GetPathToCompileCommandsOrNull(Printer print)
    {
        var ret = get_argument_or_none(Environment.CurrentDirectory, CompileCommands);
        if (ret == null)
        {
            print.Error($"Unable to locate {CompileCommand.JSON_FILE_NAME}");
        }
        return ret;

        static string? get_argument_or_none(string cwd, string? cc)
        {
            if (cc != null) { return cc; }

            var r = CompileCommand.FindBuildRootOrNull(cwd);
            if (r == null) { return null; }

            return Path.Join(r, CompileCommand.JSON_FILE_NAME);
        }
    }
}



