using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Workbench.CheckIncludes;

internal class MainCommandSettings : CommandSettings
{
    [Description("Files to look at")]
    [CommandArgument(0, "<file>")]
    public string[] files { get; set; } = Array.Empty<string>();

    [Description("Print general file status at the end")]
    [CommandOption("--status")]
    [DefaultValue(false)]
    public bool status { get; set; }

    [Description("Use verbose output")]
    [CommandOption("--verbose")]
    [DefaultValue(false)]
    public bool verbose { get; set; }
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
                (print, data) => IncludeTools.common_main(settings, print, data, new Command.MissingPatterns())
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
        public bool all { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithLoadedBuildData
            (
                (print, data) => IncludeTools.common_main(settings, print, data, new Command.ListUnfixable(settings.all == false))
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
                (print, data) => IncludeTools.common_main(settings, print, data, new Command.Check())
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
        public bool write { get; set; }
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithLoadedBuildData
            (
                (print, data) => IncludeTools.common_main(settings, print, data, new Command.Fix(settings.write == false))
            );
    }
}


public class Include : IComparable<Include>
{
    public int line_class;
    public string line;

    public Include(int line_class, string line)
    {
        this.line_class = line_class;
        this.line = line;
    }

    public int CompareTo(Include? rhs)
    {
        if(rhs == null) return 1;

        var lhs = this;
        if(lhs.line_class == rhs.line_class)
        {
            return lhs.line.CompareTo(rhs.line);
        }
        else
        {
            return lhs.line_class.CompareTo(rhs.line_class);
        }
    }
}

public enum MessageType
{
    Error, Warning
}

public static class IncludeTools
{
    private static void error(Printer print, string filename, int line, string message)
    {
        print.error($"{filename}({line}): error CHK3030: {message}");
    }

    private static void warning(Printer print, string filename, int line, string message)
    {
        print.warning($"{filename}({line}): warning CHK3030: {message}");
    }

    private static void print_message(MessageType message_type, Printer print, string filename, int line, string message)
    {
        switch(message_type)
        {
            case MessageType.Error: error(print, filename, line, message); break;
            case MessageType.Warning: warning(print, filename, line, message); break;
        }
    }

    public static TextReplacer get_replacer(string file_stem)
    {
        var replacer = new TextReplacer();
        replacer.add("{file_stem}", file_stem);
        return replacer;
    }


    private static int? classify_line
    (
        HashSet<string> missing_files,
        Printer print,
        BuildData data,
        string line,
        string filename,
        int line_num
    )
    {
        var replacer = get_replacer(Path.GetFileNameWithoutExtension(filename));

        foreach(var (index, included_regex_group) in data.includes.Select(((value, i) => (i, value))))
        {
            foreach(var included_regex in included_regex_group)
            {
                var re = included_regex.GetRegex(print, replacer);

                if(re == null) { continue; }

                if(re.Matches(line) != null)
                {
                    return index;
                }
            }
        }

        if(missing_files.Contains(line) == false)
        {
            missing_files.Add(line);
            error(print, filename, line_num, $"{line} is a invalid header");
        }

        return null;
    }



    private static string get_text_after_include_quote(string line)
    {
        var start_quote = line.IndexOf('"');
        if(start_quote == -1) { return ""; }
        
        var end_quote = line.IndexOf(line, start_quote + 1, '"');
        if(end_quote == -1) { return ""; }

        return line[(end_quote + 1)..];
    }


    private static string get_text_after_include(string line)
    {
        var angle = line.IndexOf('>');
        if(angle == -1)
        {
            return get_text_after_include_quote(line);
        }

        var start_quote = line.IndexOf('"');

        if(start_quote != -1)
        {
            if(start_quote < angle)
            {
                return get_text_after_include_quote(line);
            }
        }

        return line[(angle + 1)..];
    }


    private static ClassifiedFile? classify_file
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
        foreach(var line in lines)
        {
            line_num += 1;
        
            if(line.StartsWith("#include") == false) {  continue; }

            if(r.first_line == null)
            {
                r.first_line = line_num;
            }
            r.last_line = line_num;

            var l = line.TrimEnd();
            var line_class = classify_line(missing_files, print, data, l, filename, line_num);
            if(line_class == null) { continue; }
            r.includes.Add(new Include(line_class.Value, l));
            if(last_class > line_class.Value)
            {
                if(print_include_order_error_for_include)
                {
                    print_message(include_error_message, print, filename, line_num, $"Include order error for {l}");
                }
                r.invalid_order = true;
            }
            last_class = line_class.Value;
            if(verbose)
            {
                print.info($"{line_class.Value} {l}");
            }
        }

        r.includes.Sort();
        return r;
    }

    private static bool can_fix_and_print_errors
    (
        string[] lines,
        ClassifiedFile f,
        Printer print,
        string filename,
        bool first_error_only
    )
    {
        if(f.first_line == null || f.last_line == null)
        {
            return true;
        }

        var ok = true;

        var first_line_found = f.first_line.Value;
        var last_line_found = f.last_line.Value;
        
        for(var line_num = first_line_found; line_num < last_line_found; line_num+=1)
        {
            var print_this_error = (ok, first_error_only) switch
            {
                // this is not the first error AND we only want to print the first error
                (false, true) => false,

                // otherwise, print the error
                _ => true
            };

            var line = lines[line_num-1].Trim();

            // ignore empty
            if (string.IsNullOrEmpty(line)) { continue; }

            if(line.StartsWith("#include"))
            {
                var end = get_text_after_include(line).Trim();
                if(string.IsNullOrEmpty(end) == false && end.StartsWith("//") == false)
                {
                    if(print_this_error)
                    {
                        error(print, filename, line_num, $"Invalid text after include: {end}");
                    }
                    ok = false;
                }
            }
            else
            {
                if(print_this_error)
                {
                    error(print, filename, line_num, $"Invalid line {line}");
                }
                ok = false;
            }
        }

        return ok;
    }

    private static IEnumerable<string> generate_suggested_include_lines_from_sorted_includes(IEnumerable<Include> includes)
    {
        int? current_class = null;
        foreach(var i in includes)
        {
            if(current_class == null)
            {
                current_class = i.line_class;
            }

            if(current_class.Value != i.line_class)
            {
                yield return "";
            }
            current_class = i.line_class;
        
            yield return i.line;
        }
    }

    
    private static IEnumerable<string> compose_new_file_content(int first_line_found, int last_line_found, string[] new_lines, string[] lines)
    {
        for(int line_num=1; line_num <first_line_found; line_num++)
        {
            yield return lines[line_num-1];
        }
        foreach(var line in new_lines)
        {
            yield return line;
        }
        for(int line_num=last_line_found+1; line_num<lines.Length+1; line_num+=1)
        {
            yield return lines[line_num-1];
        }
    }

    private static void print_lines(Printer print, IEnumerable<string> lines)
    {
        print.info("*************************************************");
        foreach(var line in lines)
        {
            print.info(line);
        }
        print.info("*************************************************");
        print.info("");
        print.info("");
    }

    
    private static bool run_file
    (
        HashSet<string> missing_files,
        Printer print,
        BuildData data,
        bool verbose,
        string filename,
        Command command
    )
    {
        var command_is_list_unfixable = command is Command.ListUnfixable;
        var command_is_check          = command is Command.Check;
        var command_is_fix            = command is Command.Fix;

        var print_include_order_error_for_include = command_is_check || command_is_fix;

        if(verbose)
        {
            print.info($"Opening file {filename}");
        }
    
        var lines = Core.read_file_to_lines(filename);
        if(lines == null)
        {
            print.error($"Failed to load {filename}");
            return false;
        }

        var classified = classify_file
        (
            lines,
            missing_files,
            print,
            data,
            filename,
            verbose,
            print_include_order_error_for_include,
            command_is_fix ? MessageType.Warning : MessageType.Error
        );
        if(classified == null)
        {
            // file contains unclassified header
            return false;
        };

        if(classified.invalid_order == false)
        {
            // this file is ok, don't touch it
            return true;
        }

        if(classified.first_line == null || classified.last_line== null) { throw new Exception("bug!"); }
        // if the include order is invalid, that means there needs to be a include and we know the start and end of it
        var first_line = classified.first_line.Value;
        var last_line = classified.last_line.Value;

        if(!(command_is_fix || command_is_check || command_is_list_unfixable))
        {
            // if we wan't to print the unmatched header we don't care about sorting the headers
            return true;
        }

        bool print_first_error_only = command switch
        {
            Command.Fix => true,
            Command.ListUnfixable p => p.print_first_error_only,
            _ => false // don't care, shouldn't be possible
        };

        if(can_fix_and_print_errors(lines, classified, print, filename, print_first_error_only) == false && command_is_fix)
        {
            // can't fix this file... error out
            return false;
        }

        if(command_is_list_unfixable)
        {
            return true;
        }

        var sorted_include_lines = generate_suggested_include_lines_from_sorted_includes(classified.includes).ToArray();

        switch(command)
        {
            case Command.Fix nop:
                var file_data = compose_new_file_content(first_line, last_line, sorted_include_lines, lines);

                if(nop.nop)
                {
                    print.info($"Will write the following to {filename}");
                    print_lines(print, file_data);
                }
                else
                {
                    File.WriteAllLines(filename, file_data);
                }
                break;
            default:
                print.info("I think the correct order would be:");
                print_lines(print, sorted_include_lines);
                break;
        }

        return true;
    }

    internal static int common_main
    (
        MainCommandSettings args,
        Printer print,
        BuildData data,
        Command command
    )
    {
        var error_count = 0;
        var file_count = 0;
        var file_error = 0;

        var missing_files = new HashSet<string>();

        foreach(var filename in args.files)
        {
            file_count += 1;
            var stored_error = error_count;

            var ok = run_file
            (
                missing_files,
                print,
                data,
                args.verbose,
                filename,
                command
            );

            if(ok == false)
            {
                error_count += 1;
            }

            if(error_count != stored_error)
            {
                file_error += 1;
            }
        }

        if(args.status)
        {
            print.info($"Files parsed: {file_count}");
            print.info($"Files errored: {file_error}");
            print.info($"Errors found: {error_count}");
        }

        return error_count;
    }

}

public class ClassifiedFile
{
    public int? first_line;
    public int? last_line;
    public List<Include> includes = new();
    public bool invalid_order = false;
}


public abstract record Command
{
    public record MissingPatterns() : Command;
    public record ListUnfixable(bool print_first_error_only) : Command;
    public record Check() : Command;
    public record Fix(bool nop) : Command;
}

