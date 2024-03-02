using System;
using Workbench.Shared.Extensions;

namespace Workbench.Shared;


public record Executable
{
    public string Name { get; init; }
    public string PrimaryExecutable { get; init; }
    public string FriendlyName {get; init; }
    public string ListName {get; init; }
    public Func<IEnumerable<Found<Fil>>> Additional;

    public Executable(string primary_executable, string? name = null, Func<IEnumerable<Found<Fil>>>? additional = null)
    {
        var the_name = name ?? primary_executable;
        Name = the_name;
        PrimaryExecutable = primary_executable;
        FriendlyName = $"{the_name} executable";
        ListName = $"{the_name} executables";
        Additional = additional ?? empty;

        static IEnumerable<Found<Fil>> empty()
        {
            return Array.Empty<Found<Fil>>();
        }
    }
}

public static class DefaultExecutables
{
    public static readonly Executable Git = new ("git");
    public static readonly Executable CppLint = new("cpplint");

    public static readonly Executable ClangFormat = new ("clang-format", additional: () =>
        Functional.Params(Which.FindPaths(name => name.StartsWith("clang-format-"))));

    public static readonly Executable ClangTidy = new ("clang-tidy", additional: () =>
        Functional.Params(Which.FindPaths(name => name.StartsWith("clang-tidy-"))));

    // todo(Gustav): expand name and different executables
    public static readonly Executable Graphviz = new("dot", "graphviz", additional: ()=>
        Functional.Params(Which.FindPaths(name => name switch
        {
            "twopi" or "neato" or "sfdp" or "fdp" or "circo"
                => true,
            _ => false
        })));
}