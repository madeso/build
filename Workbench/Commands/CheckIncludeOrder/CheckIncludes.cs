using System.Text.RegularExpressions;
using Spectre.Console;
using Workbench.Config;
using Workbench.Shared;

namespace Workbench.Commands.CheckIncludeOrder;

internal record CommonArgs
(
    bool PrintStatusAtTheEnd,
    bool UseVerboseOutput
);

public readonly struct IncludeData
{
    public List<List<OptionalRegex>> IncludeDirectories { get; }

    private static IEnumerable<OptionalRegex> StringsToRegex(TextReplacer replacer, List<RegexEntry> includes, Log print) =>
        includes.Select
        (
            (Func<RegexEntry, OptionalRegex>)(regex =>
            {
                var regex_source = replacer.Replace(regex.Source);
                if (regex_source != regex.Source)
                {
                    return new OptionalRegexDynamic(regex.Source, regex.Rank);
                }
                else
                {
                    switch (CompileRegex(regex_source))
                    {
                        case RegexOrErr.Value re:
                            return new OptionalRegexStatic(re.Regex, regex.Rank);

                        case RegexOrErr.Error err:
                            var error = $"{regex} is invalid regex: {err.Message}";
                            print.Error(error);
                            return new OptionalRegexFailed(regex.Rank);
                        default:
                            throw new Exception("unhandled case");
                    }
                }
            })
        );

    internal static RegexOrErr CompileRegex(string regex_source)
    {
        try
        {
            return new RegexOrErr.Value(new Regex(regex_source, RegexOptions.Compiled));
        }
        catch (ArgumentException err)
        {
            return new RegexOrErr.Error(err.Message);
        }
    }

    public IncludeData(IEnumerable<List<RegexEntry>> includes, Log print)
    {
        var replacer = IncludeTools.CreateReplacer("file_stem");
        IncludeDirectories = includes
            .Select(single_include => StringsToRegex(replacer, single_include, print).ToList())
            .ToList();
    }

    private static IncludeData? LoadFromDirectoryOrNull(Vfs vfs, Dir cwd, Log print)
        => ConfigFile.LoadOrNull<CheckIncludesFile, IncludeData>(vfs,
            print, CheckIncludesFile.GetBuildDataPath(cwd), loaded =>
                new IncludeData(loaded.IncludeDirectories, print));

    public static IncludeData? LoadOrNull(Vfs vfs, Dir cwd, Log print)
        => LoadFromDirectoryOrNull(vfs, cwd, print);
}

public interface OptionalRegex
{
    (Regex?, int) GetRegexAndRank(Log print, TextReplacer replacer);
}

public class OptionalRegexDynamic : OptionalRegex
{
    private readonly string regex;
    private readonly int rank;

    public OptionalRegexDynamic(string regex, int rank)
    {
        this.regex = regex;
        this.rank = rank;
    }

    public (Regex?, int) GetRegexAndRank(Log print, TextReplacer replacer)
    {
        var regex_source = replacer.Replace(regex);
        switch (IncludeData.CompileRegex(regex_source))
        {
            case RegexOrErr.Value re:
                return (re.Regex, rank);
            case RegexOrErr.Error error:
                print.Error($"{regex} -> {regex_source} is invalid regex: {error.Message}");
                return (null, rank);
            default:
                throw new ArgumentException("invalid state");
        }
    }
}

public class OptionalRegexStatic : OptionalRegex
{
    private readonly Regex regex;
    private readonly int rank;

    public OptionalRegexStatic(Regex regex, int rank)
    {
        this.regex = regex;
        this.rank = rank;
    }

    public (Regex?, int) GetRegexAndRank(Log print, TextReplacer replacer)
    {
        return (regex, rank);
    }
}

public class OptionalRegexFailed : OptionalRegex
{
    private readonly int rank;

    public OptionalRegexFailed(int rank)
    {
        this.rank = rank;
    }

    public (Regex?, int) GetRegexAndRank(Log print, TextReplacer replacer)
    {
        return (null, rank);
    }
}

internal abstract record RegexOrErr
{
    public record Value(Regex Regex) : RegexOrErr;
    public record Error(string Message) : RegexOrErr;
}

public class Include : IComparable<Include>
{
    public int LineClass { get; }
    public string Line { get; }

    public Include(int line_class, string line)
    {
        LineClass = line_class;
        Line = line;
    }

    public int CompareTo(Include? rhs)
    {
        if (rhs == null) return 1;

        var lhs = this;
        if (lhs.LineClass == rhs.LineClass)
        {
            // todo(Gustav): make sure this sorts like clang format
            return string.Compare(lhs.Line, rhs.Line, StringComparison.InvariantCulture);
        }
        else
        {
            return lhs.LineClass.CompareTo(rhs.LineClass);
        }
    }
}


public static class IncludeTools
{
    private const string INCLUDE_CHECK_ERROR = "CHK3030";

    public static TextReplacer CreateReplacer(string file_stem)
    {
        var replacer = new TextReplacer();
        replacer.Add("{file_stem}", file_stem);
        return replacer;
    }


    private static int? ClassifySingleLine
    (
        HashSet<string> missing_files,
        Log print,
        IncludeData data,
        string line,
        Fil filename,
        int line_number
    )
    {
        // if the name is main.runner.cc just get the main part
        var replacer = CreateReplacer(filename.NameWithoutExtension.Split('.')[0]);

        var include_file = line;
        // remove comments
        include_file = include_file.Split("//")[0].Split("/*")[0];
        include_file = exclude('\"', include_file, '\"');
        include_file = exclude('<', include_file, '>');

        int? found_index = null;
        int best_rank = 0;

        foreach (var (index, included_regex_group) in data.IncludeDirectories.Select((value, i) => (i, value)))
        {
            foreach (var included_regex in included_regex_group)
            {
                var (re, rank) = included_regex.GetRegexAndRank(print, replacer);
                if (re == null) { continue; }

                var consider_regex = found_index == null || best_rank < rank;
                if(consider_regex && re.IsMatch(include_file))
                {
                    found_index = index;
                    best_rank = rank;
                }
            }
        }

        if (found_index != null)
        {
            return found_index;
        }

        if (missing_files.Contains(line) == false)
        {
            missing_files.Add(line);
            var message = $"Line '{line}' references a invalid header ({include_file})";
            print.PrintError(new(filename, line_number), message, INCLUDE_CHECK_ERROR);
        }

        return null;

        static string exclude(char begin, string str, char end)
        {
            var bi = str.IndexOf(begin);
            if (bi == -1) return str;
            var ei = str.IndexOf(end, bi+1);
            if(ei == -1) return str;
            return str.Substring(bi, ei-bi+1);
        }
    }


    private static string GetTextAfterInclude(string line)
    {
        var angle = line.IndexOf('>');
        if (angle == -1)
        {
            return get_text_after_quote(line);
        }

        var start_quote = line.IndexOf('"');

        if (start_quote != -1)
        {
            if (start_quote < angle)
            {
                return get_text_after_quote(line);
            }
        }

        return line[(angle + 1)..];

        static string get_text_after_quote(string line)
        {
            var start_quote = line.IndexOf('"');
            if (start_quote == -1) { return ""; }

            var end_quote = line.IndexOf('"', start_quote + 1);
            if (end_quote == -1) { return ""; }

            return line[(end_quote + 1)..];
        }
    }


    private static ClassifiedFile ClassifyFile
    (
        IEnumerable<string> lines,
        HashSet<string> missing_files,
        Log print,
        IncludeData data,
        Fil filename,
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

            r.FirstLineIndex ??= line_num;
            r.LastLineIndex = line_num;

            var l = line.TrimEnd();
            var line_class = ClassifySingleLine(missing_files, print, data, l, filename, line_num);
            if (line_class == null) { continue; }
            r.Includes.Add(new Include(line_class.Value, l));
            if (last_class > line_class.Value)
            {
                if (print_include_order_error_for_include)
                {
                    FileLine file = new (filename, line_num);
                    var message = $"Include order error for {l}";
                    print.Print(include_error_message, file, message, INCLUDE_CHECK_ERROR);
                }
                r.HasInvalidOrder = true;
            }
            last_class = line_class.Value;
            if (verbose)
            {
                AnsiConsole.WriteLine($"{line_class.Value} {l}");
            }
        }

        r.Includes.Sort();
        return r;
    }

    private static bool CanFixAndPrintErrors
    (
        string[] lines,
        ClassifiedFile f,
        Log print,
        Fil filename,
        bool print_first_error_only
    )
    {
        if (f.FirstLineIndex == null || f.LastLineIndex == null)
        {
            return true;
        }

        var ok = true;

        var first_line_found = f.FirstLineIndex.Value;
        var last_line_found = f.LastLineIndex.Value;

        for (var line_num = first_line_found; line_num < last_line_found; line_num += 1)
        {
            var print_this_error = (ok, printFirstErrorOnly: print_first_error_only) switch
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
                        var message = $"Invalid text after include: {end}";
                        print.PrintError(new(filename, line_num), message, INCLUDE_CHECK_ERROR);
                    }
                    ok = false;
                }
            }
            else
            {
                if (print_this_error)
                {
                    var message = $"Invalid line {line}";
                    print.PrintError(new(filename, line_num), message, INCLUDE_CHECK_ERROR);
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
            current_class ??= i.LineClass;

            if (current_class.Value != i.LineClass)
            {
                yield return "";
            }
            current_class = i.LineClass;

            yield return i.Line;
        }
    }


    private static IEnumerable<string> ComposeNewFileContent(int first_line_found, int last_line_found, string[] new_lines, string[] lines)
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

    private static void PrintLines(IEnumerable<string> lines)
    {
        AnsiConsole.WriteLine("*************************************************");
        foreach (var line in lines)
        {
            AnsiConsole.WriteLine(line);
        }
        AnsiConsole.WriteLine("*************************************************");
        AnsiConsole.WriteLine("");
        AnsiConsole.WriteLine("");
    }


    private static bool RunFile
    (
        Vfs vfs,
        HashSet<string> missing_files,
        Log print,
        IncludeData data,
        bool verbose,
        Fil filename,
        CheckAction command
    )
    {
        var command_is_list_unfixable = command is CheckAction.ListUnfixable;
        var command_is_check = command is CheckAction.Check;
        var command_is_fix = command is CheckAction.Fix;

        var print_include_order_error_for_include = command_is_check || command_is_fix;

        if (verbose)
        {
            AnsiConsole.WriteLine($"Opening file {filename}");
        }

        var lines = Core.ReadFileToLines(vfs, filename);
        if (lines == null)
        {
            print.Error($"Failed to load {filename}");
            return false;
        }

        var classified = ClassifyFile
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

        if (classified.HasInvalidOrder == false)
        {
            // this file is ok, don't touch it
            return true;
        }

        if (classified.FirstLineIndex == null || classified.LastLineIndex == null) { throw new Exception("bug!"); }
        // if the include order is invalid, that means there needs to be a include and we know the start and end of it
        var first_line = classified.FirstLineIndex.Value;
        var last_line = classified.LastLineIndex.Value;

        if (!(command_is_fix || command_is_check || command_is_list_unfixable))
        {
            // if we want to print the unmatched header we don't care about sorting the headers
            return true;
        }

        var print_first_error_only = command switch
        {
            CheckAction.Fix => true,
            CheckAction.ListUnfixable p => p.PrintFirstErrorOnly,
            _ => false // don't care, shouldn't be possible
        };

        if (CanFixAndPrintErrors(lines, classified, print, filename, print_first_error_only) == false && command_is_fix)
        {
            // can't fix this file... error out
            return false;
        }

        if (command_is_list_unfixable)
        {
            return true;
        }

        var sorted_include_lines = GenerateSuggestedIncludeLinesFromSortedIncludes(classified.Includes).ToArray();

        switch (command)
        {
            case CheckAction.Fix nop:
                var file_data = ComposeNewFileContent(first_line, last_line, sorted_include_lines, lines);

                if (nop.Nop)
                {
                    AnsiConsole.WriteLine($"Will write the following to {filename}");
                    PrintLines(file_data);
                }
                else
                {
                    filename.WriteAllLines(vfs, file_data);
                }
                break;
            default:
                AnsiConsole.WriteLine("I think the correct order would be:");
                PrintLines(sorted_include_lines);
                break;
        }

        return true;
    }

    internal static int CommonMain
    (
        Vfs vfs, Dir cwd, CommonArgs args,
        Log print,
        IncludeData data,
        CheckAction command
    )
    {
        var error_count = 0;
        var file_count = 0;
        var file_error = 0;

        var missing_files = new HashSet<string>();

        var files = FileUtil.FilesInPitchfork(cwd, false)
            .Where(FileUtil.IsHeaderOrSource);

        foreach (var filename in files)
        {
            file_count += 1;
            var stored_error = error_count;

            var ok = RunFile
            (
                vfs,
                missing_files,
                print,
                data,
                args.UseVerboseOutput,
                filename,
                command
            );

            if (ok == false)
            {
                error_count += 1;
            }

            if (error_count != stored_error)
            {
                file_error += 1;
            }
        }

        if (args.PrintStatusAtTheEnd)
        {
            AnsiConsole.WriteLine($"Files parsed: {file_count}");
            AnsiConsole.WriteLine($"Files errored: {file_error}");
            AnsiConsole.WriteLine($"Errors found: {error_count}");
        }

        return error_count;
    }

    public static int HandleInit(Vfs vfs, Dir cwd, Log print, bool overwrite)
    {
        var data = new CheckIncludesFile();
        data.IncludeDirectories.Add(new() { new("list of regexes"), new("that are used by check-includes" )});
        data.IncludeDirectories.Add(new() { new("they are grouped into arrays, there needs to be a space between each group" )});

        return ConfigFile.WriteInit(vfs, print, overwrite, CheckIncludesFile.GetBuildDataPath(cwd), data);
    }
}

public class ClassifiedFile
{
    public int? FirstLineIndex { get; set; }
    public int? LastLineIndex { get; set; }
    public List<Include> Includes { get; } = new();
    public bool HasInvalidOrder { get; set; } = false;
}


public abstract record CheckAction
{
    public record MissingPatterns : CheckAction;
    public record ListUnfixable(bool PrintFirstErrorOnly) : CheckAction;
    public record Check : CheckAction;
    public record Fix(bool Nop) : CheckAction;
}

