using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Workbench.CheckIncludes;

internal class MainCommandSettings : CommandSettings
{
    [Description("Files to look at")]
    [CommandArgument(0, "<file>")]
    public string[] Files { get; set; } = Array.Empty<string>();

    [Description("Print general file status at the end")]
    [CommandOption("--status")]
    [DefaultValue(false)]
    public bool PrintStatusAtTheEnd { get; set; }

    [Description("Use verbose output")]
    [CommandOption("--verbose")]
    [DefaultValue(false)]
    public bool UseVerboseOutput { get; set; }
}


internal class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, cmake =>
        {
            cmake.SetDescription("Check the order of the #include statements");
            cmake.AddCommand<MissingPatternsCommand>("missing-patterns").WithDescription("Print headers that don't match any pattern so you can add more regexes");
            cmake.AddCommand<ListUnfixableCommand>("list-unfixable").WithDescription("Print headers that can't be fixed");
            cmake.AddCommand<CheckCommand>("check").WithDescription("Check for style errors and error out");
            cmake.AddCommand<FixCommand>("fix").WithDescription("Fix style errors and print unfixable");
        });
    }
}


internal sealed class MissingPatternsCommand : Command<MissingPatternsCommand.Arg>
{
    public sealed class Arg : MainCommandSettings
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithLoadedBuildData
            (
                (print, data) => IncludeTools.CommonMain(settings, print, data, new Command.MissingPatterns())
            );
    }
}


internal sealed class ListUnfixableCommand : Command<ListUnfixableCommand.Arg>
{
    public sealed class Arg : MainCommandSettings
    {
        [Description("Print all errors per file, not just the first one")]
        [CommandOption("--all")]
        [DefaultValue(false)]
        public bool PrintAllErrors { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithLoadedBuildData
            (
                (print, data) => IncludeTools.CommonMain(settings, print, data, new Command.ListUnfixable(settings.PrintAllErrors == false))
            );
    }
}


internal sealed class CheckCommand : Command<CheckCommand.Arg>
{
    public sealed class Arg : MainCommandSettings
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithLoadedBuildData
            (
                (print, data) => IncludeTools.CommonMain(settings, print, data, new Command.Check())
            );
    }
}


internal sealed class FixCommand : Command<FixCommand.Arg>
{
    public sealed class Arg : MainCommandSettings
    {
        [Description("Write fixes to file")]
        [CommandOption("--write")]
        [DefaultValue(false)]
        public bool WriteToFile { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithLoadedBuildData
            (
                (print, data) => IncludeTools.CommonMain(settings, print, data, new Command.Fix(settings.WriteToFile == false))
            );
    }
}


public class Include : IComparable<Include>
{
    public int LineClass {get;}
    public string Line {get;}

    public Include(int line_class, string line)
    {
        this.LineClass = line_class;
        this.Line = line;
    }

    public int CompareTo(Include? rhs)
    {
        if (rhs == null) return 1;

        var lhs = this;
        if (lhs.LineClass == rhs.LineClass)
        {
            return lhs.Line.CompareTo(rhs.Line);
        }
        else
        {
            return lhs.LineClass.CompareTo(rhs.LineClass);
        }
    }
}

public enum MessageType
{
    Error, Warning
}

public static class IncludeTools
{
    private static void PrintError(Printer print, string filename, int line, string message)
    {
        print.error($"{filename}({line}): error CHK3030: {message}");
    }

    private static void PrintWarning(Printer print, string filename, int line, string message)
    {
        print.warning($"{filename}({line}): warning CHK3030: {message}");
    }

    private static void PrintMessage(MessageType messageType, Printer print, string filename, int line, string message)
    {
        switch (messageType)
        {
            case MessageType.Error: PrintError(print, filename, line, message); break;
            case MessageType.Warning: PrintWarning(print, filename, line, message); break;
        }
    }

    public static TextReplacer CreateReplacer(string file_stem)
    {
        var replacer = new TextReplacer();
        replacer.Add("{file_stem}", file_stem);
        return replacer;
    }


    private static int? ClassifySingleLine
    (
        HashSet<string> missingFiles,
        Printer print,
        BuildData data,
        string line,
        string filename,
        int lineNumber
    )
    {
        var replacer = CreateReplacer(Path.GetFileNameWithoutExtension(filename));

        foreach (var (index, included_regex_group) in data.IncludeDirectories.Select(((value, i) => (i, value))))
        {
            foreach (var included_regex in included_regex_group)
            {
                var re = included_regex.GetRegex(print, replacer);

                if (re == null) { continue; }

                if (re.Matches(line) != null)
                {
                    return index;
                }
            }
        }

        if (missingFiles.Contains(line) == false)
        {
            missingFiles.Add(line);
            PrintError(print, filename, lineNumber, $"{line} is a invalid header");
        }

        return null;
    }


    private static string GetTextAfterInclude(string line)
    {
        static string GetTextAfterQuote(string line)
        {
            var start_quote = line.IndexOf('"');
            if (start_quote == -1) { return ""; }

            var end_quote = line.IndexOf(line, start_quote + 1, '"');
            if (end_quote == -1) { return ""; }

            return line[(end_quote + 1)..];
        }

        var angle = line.IndexOf('>');
        if (angle == -1)
        {
            return GetTextAfterQuote(line);
        }

        var start_quote = line.IndexOf('"');

        if (start_quote != -1)
        {
            if (start_quote < angle)
            {
                return GetTextAfterQuote(line);
            }
        }

        return line[(angle + 1)..];
    }


    private static ClassifiedFile ClassifyFile
    (
        IEnumerable<string> lines,
        HashSet<string> missing_files,
        Printer print,
        BuildData data,
        string filename,
        bool verbose,
        bool print_include_order_error_for_include,
        MessageType include_error_message
    )
    {
        var r = new ClassifiedFile();

        var last_class = -1;
        var line_num = 0;
        foreach (var line in lines)
        {
            line_num += 1;

            if (line.StartsWith("#include") == false) { continue; }

            if (r.firstLineIndex == null)
            {
                r.firstLineIndex = line_num;
            }
            r.lastLineIndex = line_num;

            var l = line.TrimEnd();
            var line_class = ClassifySingleLine(missing_files, print, data, l, filename, line_num);
            if (line_class == null) { continue; }
            r.Includes.Add(new Include(line_class.Value, l));
            if (last_class > line_class.Value)
            {
                if (print_include_order_error_for_include)
                {
                    PrintMessage(include_error_message, print, filename, line_num, $"Include order error for {l}");
                }
                r.HasInvalidOrder = true;
            }
            last_class = line_class.Value;
            if (verbose)
            {
                print.Info($"{line_class.Value} {l}");
            }
        }

        r.Includes.Sort();
        return r;
    }

    private static bool CanFixAndPrintErrors
    (
        string[] lines,
        ClassifiedFile f,
        Printer print,
        string filename,
        bool printFirstErrorOnly
    )
    {
        if (f.firstLineIndex == null || f.lastLineIndex == null)
        {
            return true;
        }

        var ok = true;

        var first_line_found = f.firstLineIndex.Value;
        var last_line_found = f.lastLineIndex.Value;

        for (var line_num = first_line_found; line_num < last_line_found; line_num += 1)
        {
            var print_this_error = (ok, printFirstErrorOnly) switch
            {
                // this is not the first error AND we only want to print the first error
                (false, true) => false,

                // otherwise, print the error
                _ => true
            };

            var line = lines[line_num - 1].Trim();

            // ignore empty
            if (string.IsNullOrEmpty(line)) { continue; }

            if (line.StartsWith("#include"))
            {
                var end = GetTextAfterInclude(line).Trim();
                if (string.IsNullOrEmpty(end) == false && end.StartsWith("//") == false)
                {
                    if (print_this_error)
                    {
                        PrintError(print, filename, line_num, $"Invalid text after include: {end}");
                    }
                    ok = false;
                }
            }
            else
            {
                if (print_this_error)
                {
                    PrintError(print, filename, line_num, $"Invalid line {line}");
                }
                ok = false;
            }
        }

        return ok;
    }

    private static IEnumerable<string> GenerateSuggestedIncludeLinesFromSortedIncludes(IEnumerable<Include> includes)
    {
        int? current_class = null;
        foreach (var i in includes)
        {
            if (current_class == null)
            {
                current_class = i.LineClass;
            }

            if (current_class.Value != i.LineClass)
            {
                yield return "";
            }
            current_class = i.LineClass;

            yield return i.Line;
        }
    }


    private static IEnumerable<string> compose_new_file_content(int first_line_found, int last_line_found, string[] new_lines, string[] lines)
    {
        for (int line_num = 1; line_num < first_line_found; line_num++)
        {
            yield return lines[line_num - 1];
        }
        foreach (var line in new_lines)
        {
            yield return line;
        }
        for (int line_num = last_line_found + 1; line_num < lines.Length + 1; line_num += 1)
        {
            yield return lines[line_num - 1];
        }
    }

    private static void print_lines(Printer print, IEnumerable<string> lines)
    {
        print.Info("*************************************************");
        foreach (var line in lines)
        {
            print.Info(line);
        }
        print.Info("*************************************************");
        print.Info("");
        print.Info("");
    }


    private static bool RunFile
    (
        HashSet<string> missingFiles,
        Printer print,
        BuildData data,
        bool verbose,
        string filename,
        Command command
    )
    {
        var command_is_list_unfixable = command is Command.ListUnfixable;
        var command_is_check = command is Command.Check;
        var command_is_fix = command is Command.Fix;

        var print_include_order_error_for_include = command_is_check || command_is_fix;

        if (verbose)
        {
            print.Info($"Opening file {filename}");
        }

        var lines = Core.ReadFileToLines(filename);
        if (lines == null)
        {
            print.error($"Failed to load {filename}");
            return false;
        }

        var classified = ClassifyFile
        (
            lines,
            missingFiles,
            print,
            data,
            filename,
            verbose,
            print_include_order_error_for_include,
            command_is_fix ? MessageType.Warning : MessageType.Error
        );
        if (classified == null)
        {
            // file contains unclassified header
            return false;
        };

        if (classified.HasInvalidOrder == false)
        {
            // this file is ok, don't touch it
            return true;
        }

        if (classified.firstLineIndex == null || classified.lastLineIndex == null) { throw new Exception("bug!"); }
        // if the include order is invalid, that means there needs to be a include and we know the start and end of it
        var firstLine = classified.firstLineIndex.Value;
        var lastLine = classified.lastLineIndex.Value;

        if (!(command_is_fix || command_is_check || command_is_list_unfixable))
        {
            // if we wan't to print the unmatched header we don't care about sorting the headers
            return true;
        }

        bool printFirstErrorOnly = command switch
        {
            Command.Fix => true,
            Command.ListUnfixable p => p.printFirstErrorOnly,
            _ => false // don't care, shouldn't be possible
        };

        if (CanFixAndPrintErrors(lines, classified, print, filename, printFirstErrorOnly) == false && command_is_fix)
        {
            // can't fix this file... error out
            return false;
        }

        if (command_is_list_unfixable)
        {
            return true;
        }

        var sortedIncludeLines = GenerateSuggestedIncludeLinesFromSortedIncludes(classified.Includes).ToArray();

        switch (command)
        {
            case Command.Fix nop:
                var fileData = compose_new_file_content(firstLine, lastLine, sortedIncludeLines, lines);

                if (nop.nop)
                {
                    print.Info($"Will write the following to {filename}");
                    print_lines(print, fileData);
                }
                else
                {
                    File.WriteAllLines(filename, fileData);
                }
                break;
            default:
                print.Info("I think the correct order would be:");
                print_lines(print, sortedIncludeLines);
                break;
        }

        return true;
    }

    internal static int CommonMain
    (
        MainCommandSettings args,
        Printer print,
        BuildData data,
        Command command
    )
    {
        var errorCount = 0;
        var fileCount = 0;
        var fileError = 0;

        var missingFiles = new HashSet<string>();

        foreach (var filename in args.Files)
        {
            fileCount += 1;
            var storedError = errorCount;

            var ok = RunFile
            (
                missingFiles,
                print,
                data,
                args.UseVerboseOutput,
                filename,
                command
            );

            if (ok == false)
            {
                errorCount += 1;
            }

            if (errorCount != storedError)
            {
                fileError += 1;
            }
        }

        if (args.PrintStatusAtTheEnd)
        {
            print.Info($"Files parsed: {fileCount}");
            print.Info($"Files errored: {fileError}");
            print.Info($"Errors found: {errorCount}");
        }

        return errorCount;
    }

}

public class ClassifiedFile
{
    public int? firstLineIndex{get; set;}
    public int? lastLineIndex{get; set;}
    public List<Include> Includes { get; } = new();
    public bool HasInvalidOrder { get; set; } = false;
}


public abstract record Command
{
    public record MissingPatterns() : Command;
    public record ListUnfixable(bool printFirstErrorOnly) : Command;
    public record Check() : Command;
    public record Fix(bool nop) : Command;
}

