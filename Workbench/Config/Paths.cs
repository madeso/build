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

    public static Fil GetConfigFileFromCurrentDirectory(Dir cwd)
        => cwd.GetFile(FileNames.Paths);

    public static Paths? LoadConfigFromCurrentDirectoryOrNull(Dir cwd, Log? print)
    {
        return GetConfigFileFromCurrentDirectory(cwd).Exists == false
            ? new Paths()
            : ConfigFile.LoadOrNull<Paths>(print, GetConfigFileFromCurrentDirectory(cwd))
            ;
    }

    public static FoundEntry<Fil>? FindEntry(Dir cwd, Log? log, Func<Paths, Fil?> getter)
    {
        var cc = LoadConfigFromCurrentDirectoryOrNull(cwd, log);
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

    public static Found<Fil> Find(Dir cwd, Log? log, Func<Paths, Fil?> getter)
    {
        return Functional.Params(FindEntry(cwd, log, getter))
                .IgnoreNull()
                .Collect($"{FileNames.Paths} file")
            ;
    }

    public static void Save(Dir cwd, Paths p)
    {
        ConfigFile.Write(GetConfigFileFromCurrentDirectory(cwd), p);
    }

    private static IEnumerable<Found<Fil>> FindFromPath(Dir cwd, Log? log, Func<Paths, Fil?> getter) =>
        Functional.Params(Find(cwd, log, getter));
    private static IEnumerable<Found<Fil>> FindPrimaryExecutable(Executable exe) =>
        Functional.Params(Which.FindPaths(exe.PrimaryExecutable));

    internal static IEnumerable<Found<Fil>> ListAllExecutables(Dir cwd, Func<Paths, Fil?> getter, Executable exe, Log? log = null)
        =>  FindFromPath(cwd, null, getter).Concat(
            FindPrimaryExecutable(exe)).Concat(
            exe.Additional());

    internal static Fil? GetSavedOrSearchForExecutable(Dir cwd, Log? log, Func<Paths, Fil?> getter, Executable exe)
        => ListAllExecutables(cwd, getter, exe, log)
            .RequireFirstValueOrNull(log, exe.FriendlyName);



    internal static Fil? GetGitExecutable(Dir cwd, Log log)
        => GetSavedOrSearchForExecutable(cwd, log, p => p.GitExecutable, DefaultExecutables.Git);

    internal static Fil? GetClangTidyExecutable(Dir cwd, Log log)
        => GetSavedOrSearchForExecutable(cwd, log, p => p.ClangTidyExecutable, DefaultExecutables.ClangTidy);

    internal static Fil? GetClangFormatExecutable(Dir cwd, Log log)
        => GetSavedOrSearchForExecutable(cwd, log, p => p.ClangFormatExecutable, DefaultExecutables.ClangFormat);

    internal static Fil? GetCppLintExecutable(Dir cwd, Log log)
        => GetSavedOrSearchForExecutable(cwd, log, p => p.CpplintExecutable, DefaultExecutables.CppLint);

    internal static Fil? GetGraphvizExecutable(Dir cwd, Log log)
        => GetSavedOrSearchForExecutable(cwd, log, p => p.GraphvizExecutable, DefaultExecutables.Graphviz);
}
