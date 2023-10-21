using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;
using Workbench.Shared;

namespace Workbench.Commands.SlnDeps;


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
    public string Target { get; set; } = string.Empty;

    [Description("projects to exclude")]
    [CommandOption("--exclude")]
    public string[] Exclude { get; set; } = Array.Empty<string>();

    [CommandOption("--cmake")]
    [Description("exclude cmake generated projects")]
    public bool Cmake { get; set; } = false;

    [Description("projects to exclude (pattern")]
    [CommandOption("--exclude-pattern")]
    public string[] Contains { get; set; } = Array.Empty<string>();

    [CommandOption("--simplify")]
    [Description("simplify output")]
    [DefaultValue(false)]
    public bool Simplify { get; set; } = false;

    [CommandOption("--reverse")]
    [Description("reverse arrows")]
    [DefaultValue(false)]
    public bool Reverse { get; set; } = false;

    internal SlnDepsFunctions.ExclusionList MakeExclusionList()
    {
        return new SlnDepsFunctions.ExclusionList(Exclude, Contains, Cmake);
    }
}


[Description("Generate a solution dependency file")]
internal sealed class GenerateCommand : AsyncCommand<GenerateCommand.Arg>
{
    public sealed class Arg : SharedArguments
    {
        [CommandOption("--style")]
        [Description("the style")]
        [DefaultValue("")]
        public string Style {get; set;} = string.Empty;

        [CommandOption("--format")]
        [Description("the format")]
        [DefaultValue("")]
        public string Format { get; set; } = string.Empty;
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg args)
    {
        return await Log.PrintErrorsAtExitAsync(async printer =>
            await SlnDepsFunctions.HandleGenerateAsync(printer, args.Target, args.Format, args.MakeExclusionList(), args.Simplify, args.Reverse, args.Solution, args.Style)
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
        return Log.PrintErrorsAtExit(printer =>
            SlnDepsFunctions.WriteCommand(printer, args.MakeExclusionList(), args.Target, args.Simplify, args.Reverse, args.Solution)
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
        return Log.PrintErrorsAtExit(printer =>
            SlnDepsFunctions.SourceCommand(printer, args.MakeExclusionList(), args.Simplify, args.Reverse, args.Solution)
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
        return Log.PrintErrorsAtExit(printer => SlnDepsFunctions.ListCommand(printer, args.Solution));
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
