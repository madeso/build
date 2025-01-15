using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Transactions;
using Spectre.Console;
using Workbench.Shared;
using Workbench.Shared.Doxygen;
using Workbench.Shared.Extensions;
using System.Linq;
using System.Text.RegularExpressions;

namespace Workbench.Commands.ExtractWordcloud;

internal class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, git =>
        {
            git.SetDescription("Commands to extract wordcloud data");
            git.AddCommand<ExtractTypesCommand>("types");
        });
    }
}


[Description("Extract types from doxygen in arguments and return values")]
internal sealed partial class ExtractTypesCommand : Command<ExtractTypesCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Doxygen xml folder")]
        [CommandArgument(0, "<doxygen xml>")]
        public string DoxygenXml { get; init; } = string.Empty;

        [Description("output csv file")]
        [CommandArgument(0, "<csv>")]
        public string CsvOutput { get; init; } = string.Empty;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        return CliUtil.PrintErrorsAtExit(log =>
        {
            var dox = Cli.RequireDirectory(log, arg.DoxygenXml, "Doxygen xml folder");
            if (dox == null)
            {
                return -1;
            }

            AnsiConsole.WriteLine("Parsing...");
            var doxy = Doxygen.ParseIndex(dox);

            AnsiConsole.WriteLine("Collecting class functions...");
            var cc = new ColCounter<string>();

            foreach (var klass in DoxygenUtils.AllClasses(doxy))
            {
                foreach (var function in klass.SectionDefs
                             .SelectMany(x => x.MemberDef)
                             .Where(mem => mem.Kind == DoxMemberKind.Function))
                {
                    foreach (var a in function.Param)
                    {
                        if (a.Type == null) continue;
                        add_types_to_counter(cc, a.Type, klass.Language);
                    }

                    if(function.Type != null)
                    {
                        if (DoxygenUtils.IsConstructorOrDestructor(function))
                        {
                            continue;
                        }

                        add_types_to_counter(cc, function.Type, klass.Language);
                    }
                }
            }

            // todo(Gustav): list all free functions too
            var items = cc.MostCommon()
                // hacky hack to ignore void return values
                .Where(x => x.Item1 != "void");
            var output = Cli.ToSingleFile(arg.CsvOutput, "wordcloud-arguments.csv");
            output.WriteAllLines(get_csv_lines(items));

            return 0;

            static IEnumerable<string> get_csv_lines(IEnumerable<(string, int)> cc)
            {
                yield return "\"weight\";\"word\";\"color\";\"url\"";
                foreach (var (name, count) in cc)
                {
                    yield return $"\"{count}\";\"{name}\";\"\";\"\"";
                }
            }

            static void add_types_to_counter(ColCounter<string> cc, LinkedTextType a_type, DoxLanguage? lang)
            {
                var type = string.Join("", a_type.Nodes.Select(n =>
                    n switch
                    {
                        LinkedTextType.Ref r => r.Value.Extension,
                        LinkedTextType.Text text => text.Value,
                        _ => throw new ArgumentOutOfRangeException(nameof(n))
                    }
                ));
                if (lang is DoxLanguage.Cpp or DoxLanguage.Csharp)
                {
                    var t = type.Trim();
                    if (lang == DoxLanguage.Csharp)
                    {
                        t = new StringCleaner().RemoveFromStart("this", null)
                            .RemoveFromStart("out", null)
                            .RemoveFromStart("params", null)
                            .RemoveFromStart("override", null)
                            .Run(t);
                    }

                    var types = t.Split(new[] { '<', '>', '[', ']', '(', ')', ',' },
                        StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                    var sc = new StringCleaner()
                        .RemoveFromEnd("&")
                        .RemoveFromEnd("?")
                        .RemoveFromEnd("*")
                        .RemoveFromStart("const", null);

                    foreach(var tt in types)
                    {
                        var suggestion = sc.Run(tt.Trim());
                        var add = lang == DoxLanguage.Cpp
                            ? last_identifier(suggestion, "::")
                            : last_identifier(suggestion, ".");
                        if(string.IsNullOrEmpty(add)) continue;
                        if (add == ".") continue; // doxygen hack for crappy type
                        cc.AddOne(add);
                    }
                }
                else
                {
                    // todo(Gustav): add another language
                    cc.AddOne(type);
                }
            }

            static string last_identifier(string suggestion, string separator)
            {
                var last = suggestion.Split(separator)[^1];
                return SingleIdentifier().IsMatch(last)
                    ? last
                    : suggestion;
            }
        });
    }

    [GeneratedRegex("^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex SingleIdentifier();
}

class StringCleaner
{
    private record Entry(string? Start, string? End);
    private readonly List<Entry> entries = new();

    public StringCleaner RemoveFromStart(string start, string? end)
    {
        entries.Add(new Entry(start, end));
        return this;
    }

    public StringCleaner RemoveFromEnd(string end)
    {
        entries.Add(new Entry(null, end));
        return this;
    }

    public string Run(string input)
    {
        var r = input;
        while (run_once(entries, r, out var rr))
        {
            r = rr;
        }
        return r;

        static bool run_once(IEnumerable<Entry> entries, string input, out string r)
        {
            foreach (var e in entries)
            {
                if (e is { Start: not null, End: not null })
                {
                    if(input.StartsWith(e.Start) && input.EndsWith(e.End))
                    {
                        r = input[e.Start.Length..^e.End.Length].Trim();
                        return true;
                    }
                }
                else if (e is { Start: not null })
                {
                    if(input.StartsWith(e.Start))
                    {
                        r = input[e.Start.Length..].TrimStart();
                        return true;
                    }
                }
                else if (e is {End: not null })
                {
                    if(input.EndsWith(e.End))
                    {
                        r = input[..^e.End.Length].TrimEnd();
                        return true;
                    }
                }
            }

            r = input;
            return false;
        }
    }
}
