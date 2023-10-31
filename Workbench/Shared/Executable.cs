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
    public static readonly Executable ClangFormat = new ("clang-format");
    public static readonly Executable ClangTidy = new ("clang-tidy");
    public static readonly Executable CppLint = new("cpplint");

    // todo(Gustav): exapnd name and different executables
    public static readonly Executable Graphviz = new("dot", "graphviz");
}