using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Spectre.Console.Cli;
using Workbench.Shared;

namespace Workbench.Commands.CleanupOutput;

class Project
{
    public int Number { get; set; }
    public string Name { get; set; }

    public List<string> Lines { get; }= [];
}

[Description("Generate a solution dependency file")]
internal sealed class ReplaceCommand : AsyncCommand<ReplaceCommand.Arg>
{
    public sealed class Arg : CommandSettings
    {
        [Description("A file with the output of the compiler")]
        [CommandArgument(0, "<output file>")]
        public string OutputFile { get; set; } = "";

        [CommandOption("--just-replace")]
        [Description("Don't sort and instead replace the number with the name")]
        public bool DontSort { get; set; } = false;
    }

    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] Arg args)
    {
        var cwd = Dir.CurrentDirectory;
        var vfs = new VfsDisk();

        return await CliUtil.PrintErrorsAtExitAsync(async printer =>
        {
            var detect_regex =  new Regex("^([0-9]+)>-* [^:]+: *Project:([^,]+)");
            var replace_regex = new Regex("^([0-9]+)>(.*)");

            var output_file = Cli.RequireFile(vfs, cwd, printer, args.OutputFile, "output file");
            if (output_file == null)
            {
                return -1;
            }

            var lines = await vfs.ReadAllLinesAsync(output_file);

            printer.Info($"Read {lines.Length} lines");

            var name_from_number = lines
                .Select(s => detect_regex.Match(s))
                .Where(m => m.Success)
                .Select(m => new Project{ Number = int.Parse(m.Groups[1].Value.Trim()), Name = m.Groups[2].Value.Trim()})
                .ToImmutableDictionary(x => x.Number, x => x);

            printer.Info($"Found {name_from_number.Count} projects");

            if (name_from_number.IsEmpty)
            {
                printer.Error("No projects found, won't replace");
                return -1;
            }

            int replace_count = 0;
            int found_count = 0;
            int max_warnings = 10;

            var replaced = lines.Select(s =>
            {
                var f = replace_regex.Match(s);
                if (f.Success == false) return s;
                replace_count += 1;

                var num = int.Parse(f.Groups[1].Value.Trim());
                if (name_from_number.TryGetValue(num, out var project) == false)
                {
                    if (max_warnings == 0)
                    {
                        printer.Warning("Max warnings reached!");
                    }
                    if(max_warnings > 0)
                    {
                        var all_nums = string.Join(", ", name_from_number.Keys);
                        printer.Warning($"[{num}] not found in {all_nums}");
                    }
                    if(max_warnings >= 0)
                    {
                        max_warnings -= 1;
                    }
                    return s;
                }
                found_count += 1;

                var message = f.Groups[2].Value;

                project.Lines.Add(message);

                return $"{project.Name}> {message}";
            }).ToImmutableArray();

            printer.Info($"Replace matched {replace_count} time(s) and was able to replace {found_count} time(s)");

            if (replace_count <= 0 || found_count <= 0)
            {
                printer.Error("Input data seems damaged, won't replace");
                return -1;
            }


            var output_lines = args.DontSort ? replaced :
                name_from_number.Values.OrderBy(x => x.Number).SelectMany(p => p.Lines.Concat([""]));
            await vfs.WriteAllLinesAsync(output_file, output_lines);
            return 0;
        });
    }
}


internal class Main
{
    internal static void Configure(IConfigurator<CommandSettings> config, string name)
    {
        config.AddCommand<ReplaceCommand>(name).WithDescription("For a multi project build will replace numbered output with actual project name");
    }
}
