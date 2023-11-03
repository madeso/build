using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Spectre.Console;
using Workbench.Shared;
using Workbench.Shared.Doxygen;
using Workbench.Shared.Extensions;
using static Workbench.Commands.Indent.IndentationCommand;

namespace Workbench.Commands.Doc;

public class Main
{
    public static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, root =>
        {
            root.SetDescription("Documentation generator");

            root.AddCommand<RunDocCommand>("run").WithDescription("Run");
        });
    }
}

internal sealed class RunDocCommand : Command<RunDocCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Doxygen xml folder")]
        [CommandArgument(0, "<doxygen xml>")]
        public string DoxygenXml { get; init; } = string.Empty;

        [Description("Html output directory")]
        [CommandArgument(1, "<output directory>")]
        public string OutputDirectory { get; set; } = "";
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg arg)
    {
        return Log.PrintErrorsAtExit(log =>
        {
            var dox = Cli.RequireDirectory(log, arg.DoxygenXml, "Doxygen xml folder");
            if (dox == null)
            {
                return -1;
            }

            var output = Cli.ToOutputDirectory(arg.OutputDirectory);

            return DocFacade.Generate(dox, output);
        });
    }
}

internal static class DocFacade
{

    public static int Generate(Dir dox_dir, Dir root)
    {
        var dox = Doxygen.ParseIndex(dox_dir);

        foreach (var ns in DoxygenUtils.AllNamespaces(dox))
        {
            var dir = root.GetDir(namespace_name(ns.CompoundName));
            dir.CreateDir();

            foreach(var c in DoxygenUtils.IterateClassesInNamespace(dox, ns))
            {
                var f = dir.GetFile(class_name(c.CompoundName) + ".md");
                var lines = markdown_for_class(c);

                f.WriteAllLines(lines);
            }
        }

        AnsiConsole.WriteLine("Documentation written");
        return 0;

        static string replace_name(string name)
            => name
                .Replace(" ", "")
                .ToLowerInvariant()
                .GetSafeString();
        static string class_name(string name) => $"class_{replace_name(name)}";
        static string namespace_name(string name) => $"namespace_{replace_name(name)}";

        static IEnumerable<string> markdown_for_class(CompoundDef c)
        {
            yield return $"# {c.CompoundName}";
            yield return $"{to_desc(c.Briefdescription)}";
            yield return string.Empty;
            yield return string.Empty;
            yield return "-----\n";
            yield return string.Empty;
            yield return $"{to_desc(c.Detaileddescription)}";
            yield return string.Empty;
            yield return string.Empty;

            foreach (var m in DoxygenUtils.AllMembersForAClass(c))
            {
                yield return $"## {m.Type}{m.Name}{m.ArgsString}";
                yield return to_desc(m.BriefDescription);
                yield return to_desc(m.DetailedDescription);
                yield return string.Empty;
            }
        }

        static string to_desc(DescriptionType? x)
        {
            if (x == null) return string.Empty;
            var combined = x.Nodes.Select(n => n switch
            {
                DescriptionType.Text t => t.Value,
                DescriptionType.Para p => string.Join("\n", p.Value.Values.Select( xx => xx switch
                {
                    docCmdGroupText t => $"{t.Value}\n",
                    docCmdGroupProgramListing p => $"```{p.Language??string.Empty}\n{string.Join("\n", p.Lines)}\n```\n",
                    UnhandledNode u => $"UNHANDLED {u.Name}\n",
                    _ => xx.ToString()
                })),
                // todo(Gustav): handle other types
                _ => n.ToString()
            }).IgnoreNull().Where(s => s.Length > 0);
            var d = string.Join(" ", combined);
            if (string.IsNullOrEmpty(x.Title)) return d;
            else return $"## {x.Title}\n{d}";
        }
    }
}