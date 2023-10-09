// todo(Gustav): improve tree-walk eval
// todo(Gustav): include range foreach each include
// todo(Gustav): improve preproc parser so strings are excluded


using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Workbench.Utils;

namespace Workbench.ListHeaders;

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


internal class Line
{
    public string text;
    public int line;

    public Line(string text, int line)
    {
        this.text = text;
        this.line = line;
    }
}

internal class CommentStripper
{
    public readonly List<Line> ret = new();
    private string _mem = "";
    private char _last = '\0';
    private bool _singleLineComment = false;
    private bool _multiLineComment = false;
    private int _line = 1;

    private void AddLast()
    {
        if (_last != '\0')
        {
            _mem += _last;
        }
        _last = '\0';
    }

    public void Complete()
    {
        AddLast();
        if (string.IsNullOrEmpty(_mem) == false)
        {
            AddMem();
        }
    }

    private void AddMem()
    {
        ret.Add(new Line(text: _mem, line: _line));
        _mem = "";
    }

    public void Add(char c)
    {
        var last = this._last;
        if (c != '\n')
        {
            this._last = c;
        }

        if (c == '\n')
        {
            AddLast();
            AddMem();
            _line += 1;
            _singleLineComment = false;
            return;
        }
        if (_singleLineComment)
        {
            return;
        }
        if (_multiLineComment)
        {
            if (last == '*' && c == '/')
            {
                _multiLineComment = false;
            }

            return;
        }
        if (last == '/' && c == '/')
        {
            _singleLineComment = true;
        }

        if (last == '/' && c == '*')
        {
            _multiLineComment = true;
            return;
        }

        AddLast();
    }
}

internal record PreProcessor(string Command, string Arguments, int Line);

internal class PreProcessorParser
{
    private readonly List<PreProcessor> _commands;
    private int _index;

    public PreProcessorParser(List<PreProcessor> commands, int index)
    {
        _commands = commands;
        _index = index;
    }

    public bool IsValidIndex()
    {
        return _index < _commands.Count;
    }

    public PreProcessor? Peek()
    {
        return IsValidIndex()
            ? _commands[_index]
            : null;
    }

    public void Skip()
    {
        _index += 1;
    }

    public void Undo()
    {
        _index -= 1;
    }

    public PreProcessor? Next()
    {
        if (IsValidIndex())
        {
            var it = _index;
            _index += 1;
            return _commands[it];
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
    public string name;
    public string value;

    public Command(string name, string value)
    {
        this.name = name;
        this.value = value;
    }
}

internal class Elif
{
    public string condition;
    public List<Statement> block;

    public Elif(string condition, List<Statement> block)
    {
        this.condition = condition;
        this.block = block;
    }
}

internal class Block : Statement
{
    public string name;
    public string condition;
    public List<Statement> true_block = new();
    public List<Statement> false_block = new();
    public List<Elif> elifs = new();

    public Block(string name, string condition)
    {
        this.name = name;
        this.condition = condition;
    }
}

internal class FileStats
{
    public ColCounter<string> includes = new();
    public ColCounter<string> missing = new();
    public int file_count = 0;
    public int total_file_count = 0;
}

internal class FileWalker
{
    private readonly Dictionary<string, CompileCommands.CompileCommand> commands;
    public FileStats stats = new();

    public FileWalker(Dictionary<string, CompileCommands.CompileCommand> commands)
    {
        this.commands = commands;
    }

    private void add_include(string path)
    {
        var d = path;
        stats.includes.AddOne(d);
    }

    private void add_missing(string _, string include)
    {
        var iss = include;
        stats.includes.AddOne(iss);
        stats.missing.AddOne(iss);
    }

    internal bool walk
    (
        Printer print,
        string path,
        Dictionary<string, List<Statement>> file_cache
    )
    {
        print.Info($"Parsing {path}");
        stats.file_count += 1;

        if (commands.TryGetValue(path, out var cc) == false)
        {
            print.Error($"Unable to get include directories for {path}");
            return true;
        };

        var directories = cc.GetRelativeIncludes();

        var included_file_cache = new HashSet<string>();
        var defines = cc.GetDefines();

        return walk_rec(print, directories.ToArray(), included_file_cache, path, defines, file_cache, 0);
    }

    private List<Statement>? parse_file_to_blocks(string path, Printer print)
    {
        var source_lines = File.ReadAllLines(path);
        var joined_lines = F.JoinCppLines(source_lines);
        var trim_lines = joined_lines.Select(str => str.TrimStart()).ToList();
        var lines = F.RemoveCppComments(trim_lines);
        var statements = F.ParseToStatements(lines);
        var b = F.ParseToBlocks(path, print, statements.ToList());
        return b;
    }

    private bool walk_rec
    (
        Printer print,
        string[] directories,
        HashSet<string> included_file_cache,
        string path,
        Dictionary<string, string> defines,
        Dictionary<string, List<Statement>> file_cache,
        int depth
    )
    {
        stats.total_file_count += 1;

        if (FileUtil.IsSource(path))
        {
            var bblocks = parse_file_to_blocks(path, print);
            if (bblocks == null)
            {
                return false;
            }
            return block_rec(print, directories, included_file_cache, path, defines, bblocks, file_cache, depth);
        }


        if (file_cache.TryGetValue(path, out var blocks) == false)
        {
            blocks = parse_file_to_blocks(path, print) ?? new();
            file_cache.Add(path, blocks);
        }

        return block_rec(print, directories, included_file_cache, path, defines, blocks, file_cache, depth);
    }

    private bool block_rec
    (
        Printer print,
        string[] directories,
        HashSet<string> included_file_cache,
        string path,
        Dictionary<string, string> defines,
        List<Statement> blocks,
        Dictionary<string, List<Statement>> file_cache,
        int depth
    )
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Block blk:
                    switch (blk.name)
                    {
                        case "ifdef":
                        case "ifndef":
                            var key = F.SplitIdentifier(blk.condition).Item1;

                            static bool ifdef(bool t, bool f)
                            { return f && t || (!f && !t); }

                            if (blk.elifs.Count > 0)
                            {
                                // elifs are unhandled, ignoring ifdef statement"
                            }
                            else
                            {
                                if (false == block_rec
                                (
                                    print, directories, included_file_cache, path, defines,
                                    ifdef(defines.ContainsKey(key), blk.name == "ifdef")
                                        ? blk.true_block
                                        : blk.false_block
                                    , file_cache, depth
                                ))
                                {
                                    return false;
                                }
                            }
                            break;

                        default:
                            break;
                    }
                    break;
                case Command cmd:
                    switch (cmd.name)
                    {
                        case "pragma":
                            switch (cmd.value.Trim())
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
                                    print.Error($"unhandled pragma argument: {cmd.value}");
                                    break;
                            }
                            break;
                        case "define":
                            var (key, value) = F.SplitIdentifier(cmd.value);
                            defines.Add(key, value.Trim());
                            break;
                        case "undef":
                            if (defines.Remove(cmd.value.Trim()) == false)
                            {
                                print.Error($"{cmd.value} was not defined");
                            }
                            break;
                        case "include":
                            var include_name = cmd.value.Trim('"', '<', '>', ' ');
                            var sub_file = F.ResolvePath(directories, include_name, path, cmd.value.Trim().StartsWith('\"'));
                            if (sub_file != null)
                            {
                                add_include(sub_file);
                                if (false == walk_rec(print, directories, included_file_cache, sub_file, defines, file_cache, depth + 1))
                                {
                                    return false;
                                }
                            }
                            else
                            {
                                add_missing(path, include_name);
                            }
                            break;
                        case "error":
                            // nop
                            break;
                        default:
                            throw new Exception($"Unhandled statement {cmd.name}");
                    }
                    break;
            }
        }

        return true;
    }
}

// ----------------------------------------------------------------------------------------------------------------------------------------------

internal static class F
{
    internal static IEnumerable<string> JoinCppLines(IEnumerable<string> lines)
    {
        string? last_line = null;

        foreach (var line in lines)
        {
            if (line.EndsWith('\\'))
            {
                var without = line[..(line.Length - 1)];
                last_line = last_line ?? "" + without;
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

    internal static List<Line> RemoveCppComments(List<string> lines)
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

        return cs.ret;
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

    private static void GroupCommands(string path, Printer print, List<Statement> ret, PreProcessorParser commands, int depth)
    {
        while (true)
        {
            var command = commands.Next();
            if (command == null) { break; }

            if (IsIfStart(command.Command))
            {
                var group = new Block(command.Command, command.Arguments);
                GroupCommands(path, print, group.true_block, commands, depth + 1);
                while (PeekName(commands) == "elif")
                {
                    var next = commands.Next();
                    if (next == null) { throw new NullReferenceException(); }

                    var elif_args = next.Arguments;
                    var block = new List<Statement>();
                    GroupCommands(path, print, block, commands, depth + 1);
                    group.elifs.Add
                    (
                        new Elif
                        (
                            condition: elif_args,
                            block
                        )
                    );
                }
                if (PeekName(commands) == "else")
                {
                    commands.Skip();
                    GroupCommands(path, print, group.false_block, commands, depth + 1);
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
                    print.Error($"{path}({command.Line}): Ignored unmatched endif");
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
                    print.Error($"{path}({command.Line}): Ignored unmatched elif");
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
                        print.Error($"{path}({command.Line}): unknown pragma {command.Command}");
                        break;
                }
            }
        }
    }

    internal static (string, string) SplitIdentifier(string val)
    {
        var re_ident = new Regex(@"[a-zA-Z_][a-zA-Z_0-9]*");

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

    internal static IEnumerable<PreProcessor> ParseToStatements(IEnumerable<Line> lines)
    {
        foreach (var line in lines)
        {
            if (line.text.StartsWith('#'))
            {
                var li = line.text[1..].TrimStart();
                var (command, arguments) = SplitIdentifier(li);

                yield return new
                    PreProcessor
                    (
                        command,
                        arguments,
                        Line: line.line
                    );
            }
        }
    }

    internal static List<Statement> ParseToBlocks(string path, Printer print, List<PreProcessor> r)
    {
        var parser = new PreProcessorParser(commands: r, index: 0);
        var ret = new List<Statement>();
        GroupCommands(path, print, ret, parser, 0);
        return ret;
    }


    internal static void HandleLines(Printer print, string fileName, ListAction action)
    {
        var source_lines = File.ReadLines(fileName);
        var joined_lines = JoinCppLines(source_lines);
        var trim_lines = joined_lines.Select(str => str.TrimStart()).ToList();
        var lines = RemoveCppComments(trim_lines);
        switch (action)
        {
            case ListAction.Lines:
                foreach (var line in lines)
                {
                    print.Info(line.text);
                }
                break;
            case ListAction.Statements:
                {
                    var statements = ParseToStatements(lines).ToList();
                    foreach (var statement in statements)
                    {
                        print.Info($"{statement}");
                    }
                }
                break;
            case ListAction.Blocks:
                {
                    var statements = ParseToStatements(lines).ToList();
                    var blocks = ParseToBlocks(fileName, print, statements);
                    foreach (var block in blocks)
                    {
                        print.Info($"{block}");
                    }
                }
                break;
        }
    }



    internal static string? ResolvePath(string[] directories, string stem, string caller_file, bool use_relative_path)
    {
        if (use_relative_path)
        {
            var caller = new FileInfo(caller_file).Directory?.FullName;
            if (caller != null)
            {
                var r = Path.Join(caller, stem);
                if (File.Exists(r))
                {
                    return r;
                }
            }
        }

        foreach (var dd in directories)
        {
            var r = Path.Join(dd, stem);
            if (File.Exists(r))
            {
                return r;
            }
        }

        return null;
    }



    internal static int HandleFiles(Printer print, string? ccpath, List<string> sources, int mostCommonCount, bool printDebugInfo)
    {
        // var ccpath = args.GetPathToCompileCommandsOrNull(print);
        if (ccpath == null) { return -1; }

        var commands = CompileCommands.F.LoadCompileCommandsOrNull(print, ccpath);
        if (commands == null) { return -1; }

        var walker = new FileWalker(commands);

        var file_cache = new Dictionary<string, List<Statement>>();

        foreach (var file in sources)
        {
            if (File.Exists(file))
            {
                if (false == walker.walk(print, file, file_cache))
                {
                    return -1;
                }
            }
            else
            {
                var f = new DirectoryInfo(file).FullName;
                if (false == walker.walk(print, f, file_cache))
                {
                    return -1;
                }
            }
        }

        var stats = walker.stats;

        print.Info($"Top {mostCommonCount} includes are:");

        foreach (var (file, count) in stats.includes.MostCommon().Take(mostCommonCount))
        {
            var d = Path.GetRelativePath(Environment.CurrentDirectory, file);
            var times = count / (double)stats.file_count;
            print.Info($" - {d} {times:.2}x ({count}/{stats.file_count})");
        }

        return 0;
    }
}
