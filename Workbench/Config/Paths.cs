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
    public Fil? CompileCommands { get; set; }

    [JsonPropertyName("git_executable")]
    public Fil? GitExecutable { get; set; }

    [JsonPropertyName("clang_tidy_executable")]
    public Fil? ClangTidyExecutable { get; set; }

    [JsonPropertyName("clang_format_executable")]
    public Fil? ClangFormatExecutable { get; set; }
    
    [JsonPropertyName("cpp_lint_executable")]
    public Fil? CpplintExecutable { get; set; }

    [JsonPropertyName("graphviz_executable")]
    public Fil? GraphvizExecutable { get; set; }

    public static Fil GetConfigFileFromCurrentDirectory()
    {
        return Dir.CurrentDirectory.GetFile(FileNames.Paths);
    }

    public static Paths? LoadConfigFromCurrentDirectoryOrNull(Log? print)
    {
        return GetConfigFileFromCurrentDirectory().Exists == false
            ? new Paths()
            : ConfigFile.LoadOrNull<Paths>(print, GetConfigFileFromCurrentDirectory())
            ;
    }

    public static FoundEntry<Fil>? FindEntry(Log? log, Func<Paths, Fil?> getter)
    {
        var cc = LoadConfigFromCurrentDirectoryOrNull(log);
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

        return new FoundEntry<Fil>.Result(val);
    }

    public static Found<Fil> Find(Log? log, Func<Paths, Fil?> getter)
    {
        return Functional.Params(FindEntry(log, getter))
                .IgnoreNull()
                .Collect($"{FileNames.Paths} file")
            ;
    }

    public static void Save(Paths p)
    {
        ConfigFile.Write(GetConfigFileFromCurrentDirectory(), p);
    }

    private static IEnumerable<Found<Fil>> FindFromPath(Log? log, Func<Paths, Fil?> getter) =>
        Functional.Params(Find(log, getter));
    private static IEnumerable<Found<Fil>> FindPrimaryExecutable(Executable exe) =>
        Functional.Params(Which.FindPaths(exe.PrimaryExecutable));

    internal static IEnumerable<Found<Fil>> ListAllExecutables(Func<Paths, Fil?> getter, Executable exe, Log? log = null)
        =>  FindFromPath(null, getter).Concat(
            FindPrimaryExecutable(exe)).Concat(
            exe.Additional());

    internal static Fil? GetSavedOrSearchForExecutable(Log? log, Func<Paths, Fil?> getter, Executable exe)
        => ListAllExecutables(getter, exe, log)
            .RequireFirstValueOrNull(log, exe.FriendlyName);



    internal static Fil? GetGitExecutable(Log log)
        => GetSavedOrSearchForExecutable(log, p => p.GitExecutable, DefaultExecutables.Git);

    internal static Fil? GetClangTidyExecutable(Log log)
        => GetSavedOrSearchForExecutable(log, p => p.ClangTidyExecutable, DefaultExecutables.ClangTidy);

    internal static Fil? GetClangFormatExecutable(Log log)
        => GetSavedOrSearchForExecutable(log, p => p.ClangFormatExecutable, DefaultExecutables.ClangFormat);

    internal static Fil? GetCppLintExecutable(Log log)
        => GetSavedOrSearchForExecutable(log, p => p.CpplintExecutable, DefaultExecutables.CppLint);

    internal static Fil? GetGraphvizExecutable(Log log)
        => GetSavedOrSearchForExecutable(log, p => p.GraphvizExecutable, DefaultExecutables.Graphviz);
}
