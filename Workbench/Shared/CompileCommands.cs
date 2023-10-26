using System.Collections.Immutable;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Spectre.Console.Cli;
using Workbench.Config;
using Workbench.Shared.Extensions;

namespace Workbench.Shared;

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

    internal static Dictionary<string, CompileCommand>? LoadCompileCommandsOrNull(Log log, string path)
    {
        var content = File.ReadAllText(path);
        var store = JsonUtil.Parse<List<CompileCommandJson>>(log, path, content);

        if (store == null)
        {
            log.Error($"Unable to load compile commands from {path}");
            return null;
        }

        return store.ToDictionary(entry => entry.File,
            entry => new CompileCommand(directory: entry.Directory, command: entry.Command)
        );
    }

    internal const string COMPILE_COMMANDS_FILE_NAME = "compile_commands.json";

    private static IEnumerable<Found<string>> FindJustTheBuilds()
    {
        var cwd = Environment.CurrentDirectory;
        yield return FileUtil
            .PitchforkBuildFolders(cwd)
            .Select(build_root => Path.Join(build_root, COMPILE_COMMANDS_FILE_NAME))
            .Select(find_cmake_cache)
            .Collect("pitchfork folders")
            ;

        static FoundEntry<string> find_cmake_cache(string compile_commands)
        {
            if (new FileInfo(compile_commands).Exists == false)
            {
                return new FoundEntry<string>.Error($"{compile_commands} doesn't exist");
            }

            return new FoundEntry<string>.Result(compile_commands);
        }
    }

    private static FoundEntry<string>? GetBuildFromArgument(CompileCommandsArguments settings)
    {
        return settings.GetFileFromArgument(COMPILE_COMMANDS_FILE_NAME);
    }

    private static FoundEntry<string>? GetBuildFromPaths(Log? log)
    {
        var cc = Paths.LoadFromDirectoryOrNull(log)?.CompileCommands;
        if (cc == null)
        {
            // todo(Gustav): handle errors better
            return null;
        }

        return new FoundEntry<string>.Result(cc);
    }

    internal static IEnumerable<Found<string>> ListAll(CompileCommandsArguments settings)
    {
        var cmd = Functional.Params(Functional.Params(
                GetBuildFromArgument(settings))
                .IgnoreNull()
                .Collect("commandline")
        );
        var paths = Functional.Params(Functional.Params(
                GetBuildFromPaths(null))
                .IgnoreNull()
                .Collect($"{FileNames.Paths} file")
        );
        var builds = FindJustTheBuilds();

        return cmd
            .Concat(paths)
            .Concat(builds);
    }

    internal static string? RequireOrNone(CompileCommandsArguments settings, Log log)
    {
        var found = FindOrNone(settings, log);
        if (found == null)
        {
            log.Error("No compile commands specified or none/too many found");
        }

        return found;
    }

    internal static string? FindOrNone(CompileCommandsArguments settings, Log? log)
    {
        // commandline and priority paths overrides all builds
        foreach(var arg in Functional.Params(GetBuildFromArgument(settings),
                    GetBuildFromPaths(log)).IgnoreNull())
        {
            switch (arg)
            {
                case FoundEntry<string>.Result r:
                    return r.Value;
                case FoundEntry<string>.Error e:
                {
                    log?.Error(e.Reason);
                    return null;
                }
            }
        }


        var valid = FindJustTheBuilds().AllValid().ToImmutableArray();

        // only one build folder is valid
        return valid.Length == 1
            ? valid[0]
            : null;
    }

    internal static void Config(IConfigurator<CommandSettings> config, string name)
    {
        SetupPathCommand.Configure<CompileCommandsArguments>(config, name, (paths, value) => paths.CompileCommands = value, (cc) =>
        {
            CompileCommand.ListAll(cc).PrintFoundList("compile command", CompileCommand.FindOrNone(cc, null));
        }, cc => CompileCommand.ListAll(cc).SelectMany(x => x.Findings).Select(v => v.ValueOrNull).IgnoreNull().Distinct());
    }
}




internal class CompileCommandsArguments : CommandSettings
{
    [Description("the path to compile_commands.json")]
    [CommandOption("--compile-commands")]
    [DefaultValue(null)]
    public string? CompileCommands { get; set; }

    public FoundEntry<string>? GetFileFromArgument(string filename)
    {
        var settings = this;
        var found = settings.CompileCommands;
            
        if (found == null)
        {
            return null;
        }

        if (Directory.Exists(found))
        {
            // if a directory was specified, point to a file
            found = Path.Join(found, filename);
        }

        return new FoundEntry<string>.Result(found);
    }

    public FoundEntry<string>? GetDirectoryFromArgument()
    {
        var settings = this;
        var found = settings.CompileCommands;
            
        if (found == null)
        {
            return null;
        }

        if (File.Exists(found))
        {
            var dir = new FileInfo(found).Directory?.FullName;
            if (dir == null)
            {
                return new FoundEntry<string>.Error($"Failed to get directory from file {found}");
            }
        }

        return new FoundEntry<string>.Result(found);
    }
}



