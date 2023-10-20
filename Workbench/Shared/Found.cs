using System;
using System.Collections.Immutable;
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
}