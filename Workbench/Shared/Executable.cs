using System;
using Workbench.Shared.Extensions;

namespace Workbench.Shared;


public record Executable
{
    public string Name { get; init; }
    public string PrimaryExecutable { get; init; }
    public string FriendlyName {get; init; }
    public string ListName {get; init; }
    public Func<Vfs, IEnumerable<Found<Fil>>> Additional { get; }

    public Executable(string primary_executable, string? name = null, Func<Vfs, IEnumerable<Found<Fil>>>? additional = null)
    {
        var the_name = name ?? primary_executable;
        Name = the_name;
        PrimaryExecutable = primary_executable;
        FriendlyName = $"{the_name} executable";
        ListName = $"{the_name} executables";
        Additional = additional ?? empty;

        static IEnumerable<Found<Fil>> empty(Vfs _)
        {
            return Array.Empty<Found<Fil>>();
        }
    }
}

public static class DefaultExecutables
{
    public static readonly Executable Git = new ("git");
    public static readonly Executable CppLint = new("cpplint");

    public static readonly Executable ClangFormat = new ("clang-format", additional: vfs =>
        Functional.Params(Which.FindPaths(vfs, name => name.StartsWith("clang-format-"))));

    public static readonly Executable ClangTidy = new ("clang-tidy", additional: vfs =>
        Functional.Params(Which.FindPaths(vfs, name => name.StartsWith("clang-tidy-"))));

    // todo(Gustav): expand name and different executables
    public static readonly Executable Graphviz = new("dot", "graphviz", additional: vfs =>
        Functional.Params(Which.FindPaths(vfs, name => name switch
        {
            "twopi" or "neato" or "sfdp" or "fdp" or "circo"
                => true,
            _ => false
        })));
}