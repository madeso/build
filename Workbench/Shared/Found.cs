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

    public static FoundEntry<Fil> ToFoundExist(this Fil file)
    {
        if (file.Exists == false)
        {
            return new FoundEntry<Fil>.Error($"{file} doesn't exist");
        }

        return new FoundEntry<Fil>.Result(file);
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

    public static T? RequireFirstValueOrNull<T>(this IEnumerable<Found<T>> founds, Log? log, string name)
    {
        var found = GetFirstValueOrNull(founds);
        if (found == null)
        {
            log?.Error($"Failed to find valid {name}");
        }
        return found;
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

    public static T? FirstValidOrOverride<T>(this IEnumerable<Found<T>> defaults,
        IEnumerable<Found<T>> overrides, Log? log, string name)
    {
        foreach (var arg in overrides.SelectMany(x => x.Findings))
        {
            switch (arg)
            {
                case FoundEntry<T>.Result r:
                    return r.Value;
                case FoundEntry<T>.Error e:
                {
                    log?.Error(e.Reason);
                    return default;
                }
            }
        }


        var valid = defaults
            .AllValid()
            .Distinct()
            .ToImmutableArray();

        // only one build folder is valid
        if (valid.Length == 1) return valid[0];

        var found = StringListCombiner.EnglishOr().Combine(valid);
        log?.Error($"Expected 1 {name} but found {valid.Length}: {found}!");
        return default;
    }
}