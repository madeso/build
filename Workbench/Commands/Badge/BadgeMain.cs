﻿using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console.Cli;
using Workbench.Commands.Build;
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
            cmake.AddCommand<GcovrCommand>("gcovr").WithDescription("Generate badges for gcovr");
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

        [Description("Use hard corners")]
        [CommandOption("--hard-corners")]
        [DefaultValue(false)]
        public bool HardCorners { get; set; }

        [Description("Disable text stretching")]
        [CommandOption("--no-stretch")]
        [DefaultValue(false)]
        public bool DisableTextStretch { get; set; }

        [Description("The color of the name tag")]
        [CommandOption("--name")]
        [DefaultValue(BadgeColor.Grey)]
        public BadgeColor NameColor { get; set; }

        [Description("The color of the value tag")]
        [CommandOption("--value")]
        [DefaultValue(BadgeColor.Green)]
        public BadgeColor ValueColor { get; set; }

        [Description("The size of the text")]
        [CommandOption("--size")]
        [DefaultValue(11)]
        public int FontSize { get; set; }

        [Description("The corner radius")]
        [CommandOption("--radius")]
        [DefaultValue(6)]
        public float Radius { get; set; }

        [Description("The vertical padding")]
        [CommandOption("--vpad")]
        [DefaultValue(10)]
        public int VerticalPadding { get; set; } = 10;

        [Description("The horizontal padding")]
        [CommandOption("--hpad")]
        [DefaultValue(10)]
        public int HorizontalPadding { get; set; } = 10;
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
                Value = settings.Value,
                LabelColor = settings.NameColor,
                ValueColor = settings.ValueColor,
                FontSize = settings.FontSize,
                CornerRadius = settings.Radius,
                VerticalPadding = settings.VerticalPadding,
                HorizontalPadding = settings.HorizontalPadding,
            };

            if (settings.HardCorners)
            {
                b.CornerRadius = null;
            }

            b.StretchText = !settings.DisableTextStretch;

            var svg = b.GenerateSvg();
            var file = cwd.GetFile("test.svg");
            vfs.WriteAllText(file, svg);

            print.Info($"Saved file {file}");

            return 0;
        });
    }
}


internal sealed class GcovrCommand : Command<GcovrCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("Gcovr summary.json input file")]
        [CommandArgument(0, "<input>")]
        public string SummaryJson { get; set; } = "";

        [Description("output dir")]
        [CommandArgument(1, "<output>")]
        public string OutputDir { get; set; } = "";
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        var cwd = Dir.CurrentDirectory;
        var vfs = new VfsDisk();

        return CliUtil.PrintErrorsAtExit(print =>
        {
            var input = Cli.RequireFile(vfs, cwd, print, settings.SummaryJson, "input file");
            var output = Cli.ToOutputDirectory(cwd, settings.OutputDir);
            return BadgeCoverage.Convert(print, vfs, input, output);
        });
    }
}

