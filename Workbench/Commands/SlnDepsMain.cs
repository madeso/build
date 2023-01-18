using Spectre.Console.Cli;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Workbench.SlnDeps;

namespace Workbench.Commands.SlnDepsCommands;


internal class WithSolutionArguments : CommandSettings
{
    [Description("The solution to list")]
    [CommandArgument(0, "<solution>")]
    public string Solution { get; set; } = "";
}

internal class SharedArguments : WithSolutionArguments
{
    [CommandOption("--target")]
    [Description("the target")]
    [DefaultValue("")]
    public string target { get; set; } = string.Empty;

    [Description("projects to exclude")]
    [CommandOption("--exclude")]
    public string[] exclude { get; set; } = Array.Empty<string>();

    [CommandOption("--cmake")]
    [Description("exclude cmake generated projects")]
    public bool Cmake { get; set; } = false;

    [Description("projects to exclude (pattern")]
    [CommandOption("--exclude-pattern")]
    public string[] contains { get; set; } = Array.Empty<string>();

    [CommandOption("--simplify")]
    [Description("simplify output")]
    [DefaultValue(false)]
    public bool simplify { get; set; } = false;

    [CommandOption("--reverse")]
    [Description("reverse arrows")]
    [DefaultValue(false)]
    public bool reverse { get; set; } = false;

    internal F.ExclusionList MakeExclusionList()
    {
        return new F.ExclusionList(exclude, contains, Cmake);
    }
}


[Description("Generate a solution dependency file")]
internal sealed class GenerateCommand : Command<GenerateCommand.Arg>
{
    public sealed class Arg : SharedArguments
    {
        [CommandOption("--style")]
        [Description("the style")]
        [DefaultValue("")]
        public string style {get; set;} = string.Empty;

        [CommandOption("--format")]
        [Description("the format")]
        [DefaultValue("")]
        public string format { get; set; } = string.Empty;
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg args)
    {
        return CommonExecute.WithPrinter(printer =>
            F.handle_generate(printer, args.target, args.format, args.MakeExclusionList(), args.simplify, args.reverse, args.Solution, args.style)
        );
    }
}

[Description("Writes graphviz but does not run it")]
internal sealed class WriteCommand : Command<WriteCommand.Arg>
{
    public sealed class Arg : SharedArguments
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg args)
    {
        return CommonExecute.WithPrinter(printer =>
            F.handle_write(printer, args.MakeExclusionList(), args.target, args.simplify, args.reverse, args.Solution)
        );
    }
}

[Description("Display graphviz dependency file")]
internal sealed class SourceCommand : Command<SourceCommand.Arg>
{
    public sealed class Arg : SharedArguments
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg args)
    {
        return CommonExecute.WithPrinter(printer =>
            F.handle_source(printer, args.MakeExclusionList(), args.simplify, args.reverse, args.Solution)
        );
    }
}

[Description("List projects")]
internal sealed class ListCommand : Command<ListCommand.Arg>
{
    public sealed class Arg : WithSolutionArguments
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg args)
    {
        return CommonExecute.WithPrinter(printer => F.handle_list(printer, args.Solution));
    }
}


internal class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, git =>
        {
            git.SetDescription("Visual Studio solution dependency tool");
            git.AddCommand<ListCommand>("list");
            git.AddCommand<SourceCommand>("source");
            git.AddCommand<WriteCommand>("write");
            git.AddCommand<GenerateCommand>("generate");
        });
    }
}
