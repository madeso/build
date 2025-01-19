using System.Text.Json.Serialization;
using Workbench.Shared;
using Workbench.Shared.Extensions;

namespace Workbench.Config;

internal class RealPaths : Paths
{
    public Fil GetConfigFileFromCurrentDirectory(Dir cwd)
        => cwd.GetFile(FileNames.Paths);

    public SavedPaths? LoadConfigFromCurrentDirectoryOrNull(Vfs vfs, Dir cwd, Log? print)
        => GetConfigFileFromCurrentDirectory(cwd).Exists(vfs) == false
            ? new SavedPaths()
            : ConfigFile.LoadOrNull<SavedPaths>(vfs, print, GetConfigFileFromCurrentDirectory(cwd));

    public FoundEntry<Fil>? FindEntry(Vfs vfs, Dir cwd, Log? log, Func<SavedPaths, Fil?> getter)
    {
        var saved_paths = LoadConfigFromCurrentDirectoryOrNull(vfs, cwd, log);
        if (saved_paths == null)
        {
            // todo(Gustav): handle errors better
            return null;
        }

        var val = getter(saved_paths);
        if (val == null)
        {
            return null;
        }

        return new FoundEntry<Fil>.Result(val);
    }

    public override Found<Fil> Find(Vfs vfs, Dir cwd, Log? log, Func<SavedPaths, Fil?> getter)
        => Functional.Params(FindEntry(vfs, cwd, log, getter))
            .IgnoreNull()
            .Collect($"{FileNames.Paths} file");

    public void Save(Vfs vfs, Dir cwd, SavedPaths p)
        => ConfigFile.Write(vfs, GetConfigFileFromCurrentDirectory(cwd), p);

    private IEnumerable<Found<Fil>> FindFromPath(Vfs vfs, Dir cwd, Log? log, Func<SavedPaths, Fil?> getter)
        => Functional.Params(Find(vfs, cwd, log, getter));

    private IEnumerable<Found<Fil>> FindPrimaryExecutable(Vfs vfs, Executable exe)
        => Functional.Params(Which.FindPaths(vfs, exe.PrimaryExecutable));

    public override IEnumerable<Found<Fil>> ListAllExecutables(Vfs vfs, Dir cwd, Func<SavedPaths, Fil?> getter, Executable exe, Log? log = null)
        => FindFromPath(vfs, cwd, null, getter).Concat(FindPrimaryExecutable(vfs, exe)).Concat(
            exe.Additional(vfs));

    public override Fil? GetSavedOrSearchForExecutable(Vfs vfs, Dir cwd, Log? log, Func<SavedPaths, Fil?> getter, Executable exe)
        => ListAllExecutables(vfs, cwd, getter, exe, log)
            .RequireFirstValueOrNull(log, exe.FriendlyName);
}

public abstract class Paths
{
    public abstract Found<Fil> Find(Vfs vfs, Dir cwd, Log? log, Func<SavedPaths, Fil?> getter);
    public abstract IEnumerable<Found<Fil>> ListAllExecutables(Vfs vfs, Dir cwd, Func<SavedPaths, Fil?> getter, Executable exe, Log? log = null);
    public abstract Fil? GetSavedOrSearchForExecutable(Vfs vfs, Dir cwd, Log? log, Func<SavedPaths, Fil?> getter, Executable exe);

    internal Fil? GetGitExecutable(Vfs vfs, Dir cwd, Log log)
        => GetSavedOrSearchForExecutable(vfs, cwd, log, p => p.GitExecutable, DefaultExecutables.Git);

    internal Fil? GetClangTidyExecutable(Vfs vfs, Dir cwd, Log log)
        => GetSavedOrSearchForExecutable(vfs, cwd, log, p => p.ClangTidyExecutable, DefaultExecutables.ClangTidy);

    internal Fil? GetClangFormatExecutable(Vfs vfs, Dir cwd, Log log)
        => GetSavedOrSearchForExecutable(vfs, cwd, log, p => p.ClangFormatExecutable, DefaultExecutables.ClangFormat);

    internal Fil? GetCppLintExecutable(Vfs vfs, Dir cwd, Log log)
        => GetSavedOrSearchForExecutable(vfs, cwd, log, p => p.CpplintExecutable, DefaultExecutables.CppLint);

    internal Fil? GetGraphvizExecutable(Vfs vfs, Dir cwd, Log log)
        => GetSavedOrSearchForExecutable(vfs, cwd, log, p => p.GraphvizExecutable, DefaultExecutables.Graphviz);
}

public class SavedPaths
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
}
