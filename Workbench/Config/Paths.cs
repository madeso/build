using Spectre.Console.Cli;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Spectre.Console;
using Workbench.Shared;
using Workbench.Shared.Extensions;

namespace Workbench.Config;

internal class Paths
{
    [JsonPropertyName("compile_commands")]
    public string? CompileCommands { get; set; }

    [JsonPropertyName("git_executable")]
    public string? GitExecutable { get; set; }

    [JsonPropertyName("clang_tidy_executable")]
    public string? ClangTidyExecutable { get; set; }

    [JsonPropertyName("clang_format_executable")]
    public string? ClangFormatExecutable { get; set; }

    public static string GetPath()
    {
        return Path.Join(Environment.CurrentDirectory, FileNames.Paths);
    }

    public static Paths? LoadFromDirectoryOrNull(Log? print)
    {
        return File.Exists(GetPath()) == false
            ? new Paths()
            : ConfigFile.LoadOrNull<Paths>(print, GetPath())
            ;
    }

    public static FoundEntry<string>? FindEntry(Log? log, Func<Paths, string?> getter)
    {
        var cc = LoadFromDirectoryOrNull(log);
        if (cc == null)
        {
            // todo(Gustav): handle errors better
            return null;
        }

        var val = getter(cc);
        if (val == null)
        {
            return null;
        }

        return new FoundEntry<string>.Result(val);
    }

    public static Found<string> Find(Log? log, Func<Paths, string?> getter)
    {
        return Functional.Params(FindEntry(log, getter))
                .IgnoreNull()
                .Collect($"{FileNames.Paths} file")
            ;
    }

    public static void Save(Paths p)
    {
        ConfigFile.Write(GetPath(), p);
    }

    private static IEnumerable<Found<string>> ListOverrides(Log? log, Func<Paths, string?> getter) =>
        Functional.Params(Find(log, getter));
    private static IEnumerable<Found<string>> DefaultExecutable(string executable) =>
        Functional.Params(Which.FindPaths(executable));

    internal static IEnumerable<Found<string>> ListAllExecutables(Func<Paths, string?> getter, string executable)
        => ListOverrides(null, getter).Concat(DefaultExecutable(executable));

    internal static string? GetExecutableOrSaved(Log? log, Func<Paths, string?> getter, string friendly_name,
        string executable)
        => DefaultExecutable(executable).Concat(ListOverrides(log, getter))
            .RequireFirstValueOrNull(log, friendly_name);

    internal static string? GetGitExecutable(Log log)
        => GetExecutableOrSaved(log, p => p.GitExecutable, "git executable", DefaultPaths.GIT);

    internal static string? GetClangTidyExecutable(Log log)
        => GetExecutableOrSaved(log, p => p.ClangTidyExecutable, "clang tidy executable", DefaultPaths.CLANG_TIDY);

    internal static string? GetClangFormatExecutable(Log log)
        => GetExecutableOrSaved(log, p => p.ClangFormatExecutable, "clang format executable", DefaultPaths.CLANG_FORMAT);
}
