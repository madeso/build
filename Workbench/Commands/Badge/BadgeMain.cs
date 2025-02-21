using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;
using Workbench.Shared;

namespace Workbench.Commands.Badge;


internal class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, cmake =>
        {
            cmake.SetDescription("Badge commands");
            cmake.AddCommand<TestCommand>("test").WithDescription("Test the badge");
        });
    }
}

internal sealed class TestCommand : Command<TestCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Name on the label")]
        [CommandArgument(0, "<name>")]
        public string Name { get; set; } = "";

        [Description("Value to use")]
        [CommandArgument(1, "<value>")]
        public string Value { get; set; } = "";
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var cwd = Dir.CurrentDirectory;
        var vfs = new VfsDisk();

        return CliUtil.PrintErrorsAtExit(print =>
        {
            var b = new Shared.Badge()
            {
                Label = settings.Name,
                Value = settings.Value
            };

            var svg = b.GenerateSvg();
            var file = cwd.GetFile("test.svg");
            vfs.WriteAllText(file, svg);

            print.Info($"Saved file {file}");

            return 0;
        });
    }
}
