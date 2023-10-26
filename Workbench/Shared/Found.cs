using System.Collections.Immutable;
using Spectre.Console;
using Spectre.Console.Rendering;
using Workbench.Shared.Extensions;

namespace Workbench.Shared;

// todo(Gustav): expand to include more than one?
// either we want
// * the first valid one (including commandline arg) or
// * make sure there is a single in a list of many (or override with commandline arg)
// and the current design only caters to the first one
public record FoundEntry<T>
{
    public record Result(T Value) : FoundEntry<T>;
    public record Error(string Reason) : FoundEntry<T>;

    public override string ToString()
        => this switch
        {
            Result r => $"found {r.Value}",
            Error e => $"NOT FOUND ({e.Reason})",
            _ => throw new ArgumentOutOfRangeException()
        };

    public T? ValueOrNull =>
        this switch
        {
            Result r => r.Value,
            Error e => default,
            _ => throw new ArgumentOutOfRangeException()
        };
}


public record Found<T>(ImmutableArray<FoundEntry<T>> Findings, string Name)
{
    public override string ToString()
    {
        var finds = StringListCombiner.EnglishAnd().Combine(Findings);
        return $"{Name}: {finds}";
    }

    public static Found<T> Fail(string reason, string name)
        => Functional
            .Params(new FoundEntry<T>.Error(reason))
            .Collect(name);

    public static Found<T> Success(T value, string name)
        => Functional
            .Params(new FoundEntry<T>.Result(value))
            .Collect(name);
}

public static class FoundExtensions
{
    public static Found<T> Collect<T>(this IEnumerable<FoundEntry<T>> values, string name)
    {
        return new Found<T>(values.ToImmutableArray(), name);
    }

    public static IEnumerable<T> AllValid<T>(this IEnumerable<Found<T>> founds)
    {
        return founds
            .SelectMany(f => f.Findings)
            .Select(found => found switch
            {
                FoundEntry<T>.Result r => r.Value,
                _ => default
            })
            .IgnoreNull()
            ;
    }

    public static T? GetFirstValueOrNull<T>(this IEnumerable<Found<T>> founds)
    {
        return founds
            .AllValid()
            .FirstOrDefault();
    }

    internal static void PrintFoundList<T>(this IEnumerable<Found<T>> list, string name, T? selected)
    {
        var found = selected?.ToString() ?? "<None>";
        var table = new Table();

        // name/found is not exactly column title but works for now
        table.AddColumn(new TableColumn(name).RightAligned());
        table.AddColumn(new TableColumn(found).NoWrap());
        foreach (var f in list)
        {
            var renderables = f.Findings.Select<FoundEntry<T>, IRenderable>(f => f switch
            {
                FoundEntry<T>.Result r => Markup.FromInterpolated($"[blue]{r.Value}[/]"),
                FoundEntry<T>.Error e => Markup.FromInterpolated($"[red]{e.Reason}[/]"),
                _ => throw new ArgumentOutOfRangeException(nameof(f), f, null)
            }).ToList();
            if (renderables.Count == 0)
            {
                // spectre rows doesn't like empty container so hack around with a empty text
                renderables.Add(new Text(string.Empty));
            }
            var rows = new Rows(renderables);
            table.AddRow(new Text(f.Name), rows);
        }

        table.Expand();
        AnsiConsole.Write(table);
    }
}