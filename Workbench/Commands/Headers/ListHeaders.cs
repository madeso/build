// todo(Gustav): improve tree-walk eval
// todo(Gustav): include range foreach each include
// todo(Gustav): improve preproc parser so strings are excluded


using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Spectre.Console;
using Workbench.Shared;

namespace Workbench.Commands.Headers;

[TypeConverter(typeof(EnumTypeConverter<ListAction>))]
[JsonConverter(typeof(EnumJsonConverter<ListAction>))]
internal enum ListAction
{
    [EnumString("lines")]
    Lines,

    [EnumString("statements")]
    Statements,

    [EnumString("blocks")]
    Blocks,
}


// ----------------------------------------------------------------------------------------------------------------------------------------------


internal class TextLine
{
    public string Text;
    public int Line;

    public TextLine(string text, int line)
    {
        Text = text;
        Line = line;
    }
}

internal class CommentStripper
{
    public readonly List<TextLine> Result = new();
    private string memory = "";
    private char last = '\0';
    private bool single_line_comment = false;
    private bool multi_line_comment = false;
    private int line = 1;

    private void AddLast()
    {
        if (last != '\0')
        {
            memory += last;
        }
        last = '\0';
    }

    public void Complete()
    {
        AddLast();
        if (string.IsNullOrEmpty(memory) == false)
        {
            AddMem();
        }
    }

    private void AddMem()
    {
        Result.Add(new TextLine(text: memory, line: line));
        memory = "";
    }

    public void Add(char c)
    {
        if (c != '\n')
        {
            last = c;
        }

        if (c == '\n')
        {
            AddLast();
            AddMem();
            line += 1;
            single_line_comment = false;
            return;
        }
        if (single_line_comment)
        {
            return;
        }
        if (multi_line_comment)
        {
            if (last == '*' && c == '/')
            {
                multi_line_comment = false;
            }

            return;
        }
        if (last == '/' && c == '/')
        {
            single_line_comment = true;
        }

        if (last == '/' && c == '*')
        {
            multi_line_comment = true;
            return;
        }

        AddLast();
    }
}

internal record PreProcessor(string Command, string Arguments, int Line);

internal class PreProcessorParser
{
    private readonly List<PreProcessor> commands;
    private int index;

    public PreProcessorParser(List<PreProcessor> commands, int index)
    {
        this.commands = commands;
        this.index = index;
    }

    public bool IsValidIndex()
    {
        return index < commands.Count;
    }

    public PreProcessor? Peek()
    {
        return IsValidIndex()
            ? commands[index]
            : null;
    }

    public void Skip()
    {
        index += 1;
    }

    public void Undo()
    {
        index -= 1;
    }

    public PreProcessor? Next()
    {
        if (IsValidIndex())
        {
            var it = index;
            index += 1;
            return commands[it];
        }
        else
        {
            return null;
        }
    }
}

internal interface Statement
{
}

internal class Command : Statement
{
    public string Name { get; }
    public string Value { get; }

    public Command(string name, string value)
    {
        Name = name;
        Value = value;
    }
}

internal class ElseIf
{
    public string Condition { get; }
    public List<Statement> Block { get; }

    public ElseIf(string condition, List<Statement> block)
    {
        Condition = condition;
        Block = block;
    }
}

internal class Block : Statement
{
    public string Name { get; }
    public string Condition { get; }
    public List<Statement> TrueBlock { get; } = new();
    public List<Statement> FalseBlock { get; } = new();
    public List<ElseIf> ElseIfs { get; } = new();

    public Block(string name, string condition)
    {
        Name = name;
        Condition = condition;
    }
}

internal class FileStats
{
    public ColCounter<Fil> Includes = new();
    public ColCounter<string> Missing = new();
    public int FileCount = 0;
    public int TotalFileCount = 0;
}

internal class FileWalker
{
    private readonly Dictionary<Fil, CompileCommand> commands;
    public FileStats Stats = new();

    public FileWalker(Dictionary<Fil, CompileCommand> commands)
    {
        this.commands = commands;
    }

    private void AddInclude(Fil path)
    {
        Stats.Includes.AddOne(path);
    }

    private void AddMissing(string include)
    {
        // todo(Gustav): used to add missing to includes... that seems wrong... investigate!
        // Stats.Includes.AddOne(include);
        Stats.Missing.AddOne(include);
    }

    internal bool Walk
    (
        Log print, Fil path,
        Dictionary<Fil, List<Statement>> file_cache
    )
    {
        AnsiConsole.WriteLine($"Parsing {path}");
        Stats.FileCount += 1;

        if (commands.TryGetValue(path, out var cc) == false)
        {
            print.Error($"Unable to get include directories for {path}");
            return true;
        }

        var directories = cc.GetRelativeIncludes();

        var included_file_cache = new HashSet<Fil>();
        var defines = cc.GetDefines();

        return walk_rec(print, directories.ToArray(), included_file_cache, path,
            defines, file_cache, 0);
    }

    private static List<Statement> ParseFileToBlocks(Fil path, Log print)
    {
        var source_lines = path.ReadAllLines();
        var joined_lines = ListHeaderFunctions.JoinCppLines(source_lines);
        var trim_lines = joined_lines.Select(str => str.TrimStart()).ToList();
        var lines = ListHeaderFunctions.RemoveCppComments(trim_lines);
        var statements = ListHeaderFunctions.ParseToStatements(lines);
        var b = ListHeaderFunctions.ParseToBlocks(path, print, statements.ToList());
        return b;
    }

    private bool walk_rec
    (
        Log print,
        Dir[] directories,
        HashSet<Fil> included_file_cache,
        Fil path,
        Dictionary<string, string> defines,
        Dictionary<Fil, List<Statement>> file_cache,
        int depth
    )
    {
        Stats.TotalFileCount += 1;

        if (FileUtil.IsSource(path))
        {
            var parsed_blocks = ParseFileToBlocks(path, print);
            return BlockRecursive(print, directories, included_file_cache, path, defines, parsed_blocks, file_cache, depth);
        }


        if (file_cache.TryGetValue(path, out var blocks) == false)
        {
            blocks = ParseFileToBlocks(path, print);
            file_cache.Add(path, blocks);
        }

        return BlockRecursive(print, directories, included_file_cache, path, defines, blocks, file_cache, depth);
    }

    private bool BlockRecursive
    (
        Log print,
        Dir[] directories,
        HashSet<Fil> included_file_cache,
        Fil path,
        Dictionary<string, string> defines,
        List<Statement> blocks,
        Dictionary<Fil, List<Statement>> file_cache,
        int depth
    )
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Block blk:
                    switch (blk.Name)
                    {
                        case "ifdef":
                        case "ifndef":
                            var key = ListHeaderFunctions.SplitIdentifier(blk.Condition).Item1;

                            static bool ifdef(bool t, bool f)
                            { return f && t || !f && !t; }

                            if (blk.ElseIfs.Count > 0)
                            {
                                // elifs are unhandled, ignoring ifdef statement"
                            }
                            else
                            {
                                if (false == BlockRecursive
                                (
                                    print, directories, included_file_cache, path, defines,
                                    ifdef(defines.ContainsKey(key), blk.Name == "ifdef")
                                        ? blk.TrueBlock
                                        : blk.FalseBlock
                                    , file_cache, depth
                                ))
                                {
                                    return false;
                                }
                            }
                            break;
                    }
                    break;
                case Command cmd:
                    switch (cmd.Name)
                    {
                        case "pragma":
                            switch (cmd.Value.Trim())
                            {
                                case "once":
                                    var path_string = path;
                                    if (included_file_cache.Contains(path_string))
                                    {
                                        return true;
                                    }
                                    else
                                    {
                                        included_file_cache.Add(path_string);
                                    }
                                    break;
                                default:
                                    print.Error($"unhandled pragma argument: {cmd.Value}");
                                    break;
                            }
                            break;
                        case "define":
                            var (key, value) = ListHeaderFunctions.SplitIdentifier(cmd.Value);
                            defines.Add(key, value.Trim());
                            break;
                        case "undef":
                            if (defines.Remove(cmd.Value.Trim()) == false)
                            {
                                print.Error($"{cmd.Value} was not defined");
                            }
                            break;
                        case "include":
                            var include_name = cmd.Value.Trim('"', '<', '>', ' ');
                            var sub_file = ListHeaderFunctions.ResolvePath(directories, include_name, path, cmd.Value.Trim().StartsWith('\"'));
                            if (sub_file != null)
                            {
                                AddInclude(sub_file);
                                if (false == walk_rec(print, directories, included_file_cache, sub_file, defines, file_cache, depth + 1))
                                {
                                    return false;
                                }
                            }
                            else
                            {
                                AddMissing(include_name);
                            }
                            break;
                        case "error":
                            // nop
                            break;
                        default:
                            throw new Exception($"Unhandled statement {cmd.Name}");
                    }
                    break;
            }
        }

        return true;
    }
}

// ----------------------------------------------------------------------------------------------------------------------------------------------

internal static class ListHeaderFunctions
{
    internal static IEnumerable<string> JoinCppLines(IEnumerable<string> lines)
    {
        string? last_line = null;

        foreach (var line in lines)
        {
            if (line.EndsWith('\\'))
            {
                var without = line[..^1];
                last_line ??= "" + without;
            }
            else if (last_line != null)
            {
                yield return last_line + line;
                last_line = null;
            }
            else
            {
                yield return line;
            }
        }

        if (last_line != null)
        {
            yield return last_line;
        }
    }

    internal static List<TextLine> RemoveCppComments(List<string> lines)
    {
        var cs = new CommentStripper();
        foreach (var line in lines)
        {
            foreach (var c in line)
            {
                cs.Add(c);
            }
            cs.Add('\n');
        }

        cs.Complete();

        return cs.Result;
    }

    private static bool IsIfStart(string name)
    {
        return name is "if" or "ifdef" or "ifndef";
    }

    private static string PeekName(PreProcessorParser commands)
    {
        var peeked = commands.Peek();
        return peeked != null
            ? peeked.Command
            : string.Empty
            ;
    }

    private static void GroupCommands(Fil path, Log print, List<Statement> ret, PreProcessorParser commands, int depth)
    {
        while (true)
        {
            var command = commands.Next();
            if (command == null) { break; }

            if (IsIfStart(command.Command))
            {
                var group = new Block(command.Command, command.Arguments);
                GroupCommands(path, print, group.TrueBlock, commands, depth + 1);
                while (PeekName(commands) == "elif")
                {
                    var next = commands.Next();
                    if (next == null) { throw new NullReferenceException(); }

                    var elif_args = next.Arguments;
                    var block = new List<Statement>();
                    GroupCommands(path, print, block, commands, depth + 1);
                    group.ElseIfs.Add
                    (
                        new ElseIf
                        (
                            condition: elif_args,
                            block
                        )
                    );
                }
                if (PeekName(commands) == "else")
                {
                    commands.Skip();
                    GroupCommands(path, print, group.FalseBlock, commands, depth + 1);
                }
                if (PeekName(commands) == "endif")
                {
                    commands.Skip();
                }
                else
                {
                    // nop
                }
                ret.Add(group);
            }
            else if (command.Command == "else")
            {
                commands.Undo();
                return;
            }
            else if (command.Command == "endif")
            {
                if (depth > 0)
                {
                    commands.Undo();
                    return;
                }
                else
                {
                    print.Error(new(path, command.Line), "Ignored unmatched endif");
                }
            }
            else if (command.Command == "elif")
            {
                if (depth > 0)
                {
                    commands.Undo();
                    return;
                }
                else
                {
                    print.Error(new(path, command.Line), "Ignored unmatched elif");
                }
            }
            else
            {
                switch (command.Command)
                {
                    case "define":
                    case "error":
                    case "include":
                    case "pragma":
                    case "undef":
                        ret.Add(new Command(
                            name: command.Command,
                            value: command.Arguments
                        ));
                        break;
                    case "version":
                        // todo(Gustav): glsl verbatim string, ignore foreach now
                        // pass
                        break;
                    default:
                        print.Error(new(path, command.Line), $"unknown pragma {command.Command}");
                        break;
                }
            }
        }
    }

    internal static (string, string) SplitIdentifier(string val)
    {
        var re_ident = new Regex("[a-zA-Z_][a-zA-Z_0-9]*");

        var f = re_ident.Match(val);
        if (f.Success)
        {
            var capt = f.Captures[0];
            var key = capt.Value;
            var end = capt.Index + capt.Length;
            var value = val[end..];
            return (key, value);
        }
        else
        {
            return (val, "");
        }
    }

    internal static IEnumerable<PreProcessor> ParseToStatements(IEnumerable<TextLine> lines)
    {
        foreach (var line in lines)
        {
            if (line.Text.StartsWith('#'))
            {
                var li = line.Text[1..].TrimStart();
                var (command, arguments) = SplitIdentifier(li);

                yield return new
                    PreProcessor
                    (
                        command,
                        arguments,
                        Line: line.Line
                    );
            }
        }
    }

    internal static List<Statement> ParseToBlocks(Fil path, Log print, List<PreProcessor> r)
    {
        var parser = new PreProcessorParser(commands: r, index: 0);
        var ret = new List<Statement>();
        GroupCommands(path, print, ret, parser, 0);
        return ret;
    }


    internal static void HandleLines(Log print, Fil file_name, ListAction action)
    {
        var source_lines = file_name.ReadAllLines();
        var joined_lines = JoinCppLines(source_lines);
        var trim_lines = joined_lines.Select(str => str.TrimStart()).ToList();
        var lines = RemoveCppComments(trim_lines);
        switch (action)
        {
            case ListAction.Lines:
                foreach (var line in lines)
                {
                    AnsiConsole.WriteLine(line.Text);
                }
                break;
            case ListAction.Statements:
                {
                    var statements = ParseToStatements(lines).ToList();
                    foreach (var statement in statements)
                    {
                        AnsiConsole.WriteLine($"{statement}");
                    }
                }
                break;
            case ListAction.Blocks:
                {
                    var statements = ParseToStatements(lines).ToList();
                    var blocks = ParseToBlocks(file_name, print, statements);
                    foreach (var block in blocks)
                    {
                        AnsiConsole.WriteLine($"{block}");
                    }
                }
                break;
        }
    }



    internal static Fil? ResolvePath(Dir[] directories, string stem, Fil caller_file,
        bool use_relative_path)
    {
        if (use_relative_path)
        {
            var caller = caller_file.Directory?.GetFile(stem);
            if (caller is { Exists: true })
            {
                return caller;
            }
        }

        return directories
            .Select(dd => dd.GetFile(stem))
            .FirstOrDefault(r => r.Exists);
    }



    internal static int HandleFiles(
        Log print, Fil? ccpath, IEnumerable<FileOrDir> sources, int most_common_count)
    {
        if (ccpath == null) { return -1; }

        var commands = CompileCommand.LoadCompileCommandsOrNull(print, ccpath);
        if (commands == null) { return -1; }

        var walker = new FileWalker(commands);

        var file_cache = new Dictionary<Fil, List<Statement>>();

        foreach (var file in sources)
        {
            var ff = new Fil(file.Path);
            if (ff.Exists)
            {
                if (false == walker.Walk(print, ff, file_cache))
                {
                    return -1;
                }
            }
            else
            {
                foreach(var fi in new Dir(file.Path).EnumerateFiles())
                {
                    if (false == walker.Walk(print, fi, file_cache))
                    {
                        return -1;
                    }
                }
            }
        }

        var stats = walker.Stats;

        AnsiConsole.WriteLine($"Top {most_common_count} includes are:");

        foreach (var (file, count) in stats.Includes.MostCommon().Take(most_common_count))
        {
            var d = file.GetDisplay();
            var times = count / (double)stats.FileCount;
            AnsiConsole.WriteLine($" - {d} {times:.2}x ({count}/{stats.FileCount})");
        }

        return 0;
    }
}
