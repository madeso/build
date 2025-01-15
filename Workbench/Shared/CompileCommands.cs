using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Spectre.Console.Cli;
using Workbench.Config;
using Workbench.Shared.Extensions;

namespace Workbench.Shared;

public class CompileCommand
{
    public Dir Directory;
    public string Command;

    public CompileCommand(Dir directory, string command)
    {
        Directory = directory;
        Command = command;
    }

    public IEnumerable<Dir> GetRelativeIncludes()
    {
        // shitty commandline parser... beware
        foreach (var c in Command.Split(' '))
        {
            const string INCLUDE_PREFIX = "-I";
            if (c.StartsWith(INCLUDE_PREFIX) == false) { continue; }

            yield return Directory.GetDir(c[INCLUDE_PREFIX.Length..].Trim());
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

    internal static Dictionary<Fil, CompileCommand>? LoadCompileCommandsOrNull(Log log, Fil path)
    {
        var content = path.ReadAllText();
        var store = JsonUtil.Parse<List<CompileCommandJson>>(log, path, content);

        if (store == null)
        {
            log.Error($"Unable to load compile commands from {path}");
            return null;
        }

        return store.ToDictionary(entry => new Fil(entry.File),
            entry => new CompileCommand(directory: new Dir(entry.Directory), command: entry.Command)
        );
    }

    internal const string COMPILE_COMMANDS_FILE_NAME = "compile_commands.json";

    private static IEnumerable<Found<Fil>> FindJustTheBuilds(Dir cwd)
    {
        yield return FileUtil
            .PitchforkBuildFolders(cwd)
            .Select(build_root => build_root.GetFile(COMPILE_COMMANDS_FILE_NAME))
            .Select(f => f.ToFoundExist())
            .Collect("pitchfork folders")
            ;
    }

    private static FoundEntry<Fil>? GetBuildFromArgument(CompileCommandsArguments settings)
    {
        return settings.GetFileFromArgument(COMPILE_COMMANDS_FILE_NAME);
    }

    internal static IEnumerable<Found<Fil>> ListOverrides(Paths paths, Dir cwd, CompileCommandsArguments settings, Log? log)
    {
        yield return Functional.Params(
                    GetBuildFromArgument(settings))
                .IgnoreNull()
                .Collect("commandline")
            ;
        yield return paths.Find(cwd, log, p => p.CompileCommands);
    }

    internal static IEnumerable<Found<Fil>> ListAll(Dir cwd, CompileCommandsArguments settings, Paths paths)
        => ListOverrides(paths, cwd, settings, null)
            .Concat(FindJustTheBuilds(cwd));

    internal static Fil? FindOrNone(Dir cwd, CompileCommandsArguments settings, Log? log, Paths paths)
    {
        return FindJustTheBuilds(cwd)
            .FirstValidOrOverride(ListOverrides(paths, cwd, settings, log), log, "compile command");
    }
}

public class CompileCommandsArguments : CommandSettings
{
    [Description("the path to compile_commands.json")]
    [CommandOption("--compile-commands")]
    [DefaultValue(null)]
    public string? CompileCommands { get; set; }

    public FoundEntry<Fil>? GetFileFromArgument(string filename)
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

        return new FoundEntry<Fil>.Result(new Fil(found));
    }

    public FoundEntry<Dir>? GetDirectoryFromArgument()
    {
        //  todo(Gustav): merge with cli

        var settings = this;
        var arg = settings.CompileCommands;
            
        if (arg == null)
        {
            return null;
        }

        var file = new Fil(arg);
        if(file.Exists)
        {
            var dir = file.Directory;
            if (dir == null)
            {
                return new FoundEntry<Dir>.Error($"Failed to get directory from file {arg}");
            }

            return new FoundEntry<Dir>.Result(dir);
        }

        return new FoundEntry<Dir>.Result(new Dir(arg));
    }
}



