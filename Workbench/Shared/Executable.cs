using Workbench.Shared.Extensions;

namespace Workbench.Shared;


public record Executable
{
    public string Name { get; init; }
    public string PrimaryExecutable { get; init; }
    public string FriendlyName {get; init; }
    public string ListName {get; init; }

    public Executable(string primary_executable)
    {
        Name = primary_executable;
        PrimaryExecutable = primary_executable;
        FriendlyName = $"{primary_executable} executable";
        ListName = $"{primary_executable} executables";
    }

    public Executable(string primary_executable, string name)
    {
        Name = name;
        PrimaryExecutable = primary_executable;
        FriendlyName = $"{name} executable";
        ListName = $"{name} executables";
    }
}

public static class DefaultExecutables
{
    public static readonly Executable Git = new ("git");
    public static readonly Executable CppLint = new("cpplint");

    public static readonly Executable ClangFormat = new ("clang-format");
    public static IEnumerable<Found<Fil>> ClangFormatExtra
        = Functional.Params(Which.FindPaths(name => name.StartsWith("clang-format-")));

    public static readonly Executable ClangTidy = new ("clang-tidy");
    public static IEnumerable<Found<Fil>> ClangTidyExtra =
        Functional.Params(Which.FindPaths(name => name.StartsWith("clang-tidy-")));

    // todo(Gustav): expand name and different executables
    public static readonly Executable Graphviz = new("dot", "graphviz");
    public static IEnumerable<Found<Fil>> GraphvizExtra = Functional.Params(Which.FindPaths(name => name switch
    {
        "twopi" or "neato" or "sfdp" or "fdp" or "circo"
            => true,
        _ => false
    }));
}