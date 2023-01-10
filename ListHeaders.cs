// todo(Gustav): improve tree-walk eval
// todo(Gustav): include range foreach each include
// todo(Gustav): improve preproc parser so strings are excluded


using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;
using System.Windows.Markup;

namespace Workbench.ListHeaders;

internal class LinesArg : CommandSettings
{
    [Description("File to list lines in")]
    [CommandArgument(0, "<filename>")]
    public string filename { get; set; } = string.Empty;

    [Description("List statements instead")]
    [CommandOption("--statements")]
    [DefaultValue(false)]
    public bool statements { get; set; }

    [Description("List blocks instead")]
    [CommandOption("--blocks")]
    [DefaultValue(false)]
    public bool blocks { get; set; }
}


// #[derive(StructOpt, Debug)]
internal class FilesArg : CompileCommands.MainCommandSettings
{
    [Description("project file")]
    [CommandArgument(0, "<source>")]
    public List<string> sources { get; set; } = new();

    [Description("number of most common includes to print")]
    [CommandOption("--count")]
    [DefaultValue(10)]
    public int count { get; set; }

    [Description("print debug info")]
    [CommandOption("--debug")]
    [DefaultValue(false)]
    public bool debug { get; set; }
}



internal sealed class LinesCommand : Command<LinesCommand.Arg>
{
    public sealed class Arg : LinesArg
    {
        [Description("Git root to use")]
        [CommandArgument(0, "<git root>")]
        public string Root { get; set; } = "";
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter
            (
                print => { F.handle_lines(print, settings); return 0; }
            );
    }
}

internal sealed class FilesCommand : Command<FilesCommand.Arg>
{
    public sealed class Arg : FilesArg
    {
    }

    public override int Execute([NotNull] CommandContext context, [NotNull] Arg settings)
    {
        return CommonExecute.WithPrinter(print => F.handle_files(print, settings) ? 0 : -1);
    }
}


internal class Main
{
    internal static void Configure(IConfigurator config, string name)
    {
        config.AddBranch(name, git =>
        {
            git.SetDescription("Tool to list headers");
            git.AddCommand<LinesCommand>("lines").WithDescription("List lines in a single file");
            git.AddCommand<FilesCommand>("files").WithDescription("Display includeded files from one or more source files");
        });
    }
}

// ----------------------------------------------------------------------------------------------------------------------------------------------


class Line
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
    public readonly List<Line> ret = new List<Line>();
    string mem = "";
    char last = '\0';
    bool single_line_comment = false;
    bool multi_line_comment = false;
    int line = 1;

    void add_last()
    {
        if(this.last != '\0')
        {
            this.mem += this.last;
        }
        this.last = '\0';
    }

    public void complete()
    {
        this.add_last();
        if(string.IsNullOrEmpty(this.mem) == false)
        {
            this.add_mem();
        }
    }

    void add_mem()
    {
        this.ret.Add(new Line(text: this.mem, line: this.line));
        this.mem = "";
    }

    public void add(char c)
    {
        var last = this.last;
        if(c != '\n')
        {
            this.last = c;
        }

        if(c == '\n')
        {
            this.add_last();
            this.add_mem();
            this.line += 1;
            this.single_line_comment = false;
            return;
        }
        if(this.single_line_comment)
        {
            return;
        }
        if(this.multi_line_comment)
        {
            if(last == '*' && c == '/')
            {
                this.multi_line_comment = false;
            }

            return;
        }
        if(last == '/' && c == '/')
        {
            this.single_line_comment = true;
        }

        if(last == '/' && c == '*')
        {
            this.multi_line_comment = true;
            return;
        }

        this.add_last();
    }
}

class Preproc
{
    public string command {get; init;}
    public string arguments {get; init;}
    public int line { get; init; }

    public Preproc(string command, string arguments, int line)
    {
        this.command = command;
        this.arguments = arguments;
        this.line = line;
    }
}

class PreprocParser
{
    List<Preproc> commands;
    int index;

    public PreprocParser(List<Preproc> commands, int index)
    {
        this.commands = commands;
        this.index = index;
    }

    public bool validate_index()
    {
        return this.index < this.commands.Count;
    }
    
    public Preproc? opeek()
    {
        if(this.validate_index())
        {
            return this.commands[this.index];
        }
        else
        {
            return null;
        }
    }
    
    public void skip()
    {
        this.index += 1;
    }
    
    public void undo()
    {
        this.index -= 1;
    }

    public Preproc? next()
    {
        if(this.validate_index())
        {
            var it = this.index;
            this.index += 1;
            return this.commands[it];
        }
        else
        {
            return null;
        }
    }
}


interface Statement
{
}

class Command : Statement
{
    public string name;
    public string value;

    public Command(string name, string value)
    {
        this.name = name;
        this.value = value;
    }
}

class Elif
{
    public string condition;
    public List<Statement> block;

    public Elif(string condition, List<Statement> block)
    {
        this.condition = condition;
        this.block = block;
    }
}

class Block : Statement
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

class FileStats
{
    public ColCounter<string> includes = new();
    public ColCounter<string> missing = new();
    public int file_count = 0;
    public int total_file_count = 0;
}


class FileWalker
{
    Dictionary<string, CompileCommands.CompileCommand> commands;
    public FileStats stats = new();

    public FileWalker(Dictionary<string, CompileCommands.CompileCommand> commands)
    {
        this.commands = commands;
    }

    void add_include(string path)
    {
        var d = path;
        this.stats.includes.AddOne(d);
    }

    void add_missing(string _, string include)
    {
        var iss = include;
        this.stats.includes.AddOne(iss);
        this.stats.missing.AddOne(iss);
    }

    internal bool walk
    (
        Printer print,
        string path,
        Dictionary<string, List<Statement>> file_cache
    )
    {
        print.info($"Parsing {path}");
        this.stats.file_count += 1;

        if (this.commands.TryGetValue(path, out var cc) == false)
        {
            print.error($"Unable to get include directories foreach {path}");
            return true;
        };

        var directories = cc.get_relative_includes();

        var included_file_cache = new HashSet<string>();
        var defines = cc.get_defines();

        return this.walk_rec(print, directories.ToArray(), included_file_cache, path, defines, file_cache, 0);
    }

    List<Statement>? parse_file_to_blocks(string path, Printer print)
    {
        var source_lines = File.ReadAllLines(path);
        var joined_lines = F.join_lines(source_lines);
        var trim_lines = joined_lines.Select(str => str.TrimStart()).ToList();
        var lines = F.remove_cpp_comments(trim_lines);
        var statements = F.parse_to_statements(lines);
        var b = F.parse_to_blocks(path, print, statements.ToList());
        return b;
    }

    bool walk_rec
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
        this.stats.total_file_count += 1;

        if (F.is_source(path))
        {
            var bblocks = this.parse_file_to_blocks(path, print);
            if(bblocks == null)
            {
                return false;
            }
            return this.block_rec(print, directories, included_file_cache, path, defines, bblocks, file_cache, depth);
        }

        
        if (file_cache.TryGetValue(path, out var blocks) == false)
        {
            blocks = this.parse_file_to_blocks(path, print) ?? new();
            file_cache.Add(path, blocks);
        }

        return this.block_rec(print, directories, included_file_cache, path, defines, blocks, file_cache, depth);
    }

    bool block_rec
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
                            var key = F.ident_split(blk.condition).Item1;

                            static bool ifdef(bool t, bool f)
                            { return f && t || (!f && !t); }

                            if (blk.elifs.Count > 0)
                            {
                                // elifs are unhandled, ignoring ifdef statement"
                            }
                            else
                            {
                                if (false == this.block_rec
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
                                    print.error($"unhandled pragma argument: {cmd.value}");
                                    break;
                            }
                            break;
                        case "define":
                            var (key, value) = F.ident_split(cmd.value);
                            defines.Add(key, value.Trim());
                            break;
                        case "undef":
                            if (defines.Remove(cmd.value.Trim()) == false)
                            {
                                print.error($"{cmd.value} was not defined");
                            }
                            break;
                        case "include":
                            var include_name = cmd.value.Trim('"', '<', '>', ' ');
                            var sub_file = F.resolve_path(directories, include_name, path, cmd.value.Trim().StartsWith('\"'));
                            if (sub_file != null)
                            {
                                this.add_include(sub_file);
                                if (false == this.walk_rec(print, directories, included_file_cache, sub_file, defines, file_cache, depth + 1))
                                {
                                    return false;
                                }
                            }
                            else
                            {
                                this.add_missing(path, include_name);
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
    internal static IEnumerable<string> join_lines(IEnumerable<string> lines)
    {
        string? last_line = null;
    
        foreach(var line in lines)
        {
            if(line.EndsWith('\\'))
            {
                var without = line[..(line.Length - 1)];
                last_line = last_line??"" + without;
            }
            else if(last_line != null)
            {
                yield return last_line + line;
                last_line = null;
            }
            else
            {
                yield return line;
            }
        }

        if(last_line != null)
        {
            yield return last_line;
        }
    }

    internal static List<Line> remove_cpp_comments(List<string> lines)
    {
        var cs = new CommentStripper();
        foreach(var line in lines)
        {
            foreach(var c in line)
            {
                cs.add(c);
            }
            cs.add('\n');
        }

        cs.complete();

        return cs.ret;
    }

    static bool is_if_start(string name)
    {
        return name == "if" || name == "ifdef" || name == "ifndef";
    }

    static string peek_name(PreprocParser commands)
    {
        var peeked = commands.opeek();
        return peeked != null
            ? peeked.command
            : string.Empty
            ;
    }

    
    static void group_commands(string path, Printer print, List<Statement> ret, PreprocParser commands, int depth)
    {
        while(true)
        {
            var command = commands.next();
            if(command == null ) { break; }

            if(is_if_start(command.command))
            {
                var group = new Block(command.command, command.arguments);
                group_commands(path, print, group.true_block, commands, depth+1);
                while(peek_name(commands) == "elif")
                {
                    var next = commands.next();
                    if(next == null) { throw new NullReferenceException(); }

                    var elif_args = next.arguments;
                    var block = new List<Statement>();
                    group_commands(path, print, block, commands, depth+1);
                    group.elifs.Add
                    (
                        new Elif
                        (
                            condition: elif_args,
                            block
                        )
                    );
                }
                if(peek_name(commands) == "else")
                {
                    commands.skip();
                    group_commands(path, print, group.false_block, commands, depth+1);
                }
                if(peek_name(commands) == "endif")
                {
                    commands.skip();
                }
                else
                {
                    // nop
                }
                ret.Add(group);
            }
            else if(command.command == "else")
            {
                commands.undo();
                return;
            }
            else if(command.command == "endif")
            {
                if(depth > 0)
                {
                    commands.undo();
                    return;
                }
                else
                {
                    print.error($"{path}({command.line}): Ignored unmatched endif");
                }
            }
            else if(command.command == "elif")
            {
                if(depth > 0)
                {
                    commands.undo();
                    return;
                }
                else
                {
                    print.error($"{path}({command.line}): Ignored unmatched elif");
                }
            }
            else
            {
                switch(command.command)
                {
                    case "define": case "error": case "include": case "pragma": case "undef":
                        ret.Add(new Command(
                            name: command.command,
                            value: command.arguments
                        ));
                        break;
                    case "version":
                        // todo(Gustav): glsl verbatim string, ignore foreach now
                        // pass
                        break;
                    default:
                        print.error($"{path}({command.line}): unknown pragma {command.command}");
                        break;
                }
            }
        }
    }

    internal static (string, string) ident_split(string val)
    {
        var re_ident = new Regex(@"[a-zA-Z_][a-zA-Z_0-9]*");

        var f = re_ident.Match(val);
        if(f.Success)
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
    
    internal static IEnumerable<Preproc> parse_to_statements(IEnumerable<Line> lines)
    {
        foreach(var line in lines)
        {
            if(line.text.StartsWith('#'))
            {
                var li = line.text[1..].TrimStart();
                var (command, arguments) = ident_split(li);

                yield return new
                    Preproc
                    (
                        command,
                        arguments,
                        line: line.line
                    );
            }
        }
    }

    internal static List<Statement> parse_to_blocks(string path, Printer print, List<Preproc> r)
    {
        var parser = new PreprocParser(commands: r, index: 0);
        var ret = new List<Statement>();
        group_commands(path, print, ret, parser, 0);
        return ret;
    }

    
    internal static void handle_lines(Printer print, LinesArg args)
    {
        var source_lines = File.ReadLines(args.filename);
        var joined_lines = join_lines(source_lines);
        var trim_lines = joined_lines.Select(str => str.TrimStart()).ToList();
        var lines = remove_cpp_comments(trim_lines);
        if(args.statements || args.blocks)
        {
            var statements = parse_to_statements(lines).ToList();

            if(args.blocks)
            {
                var blocks = parse_to_blocks(args.filename, print, statements);
                foreach(var block in blocks)
                {
                    print.info($"{block}");
                }
            }
            else
            {
                foreach(var statement in statements)
                {
                    print.info($"{statement}");
                }
            }
        }
        else
        {
            foreach(var line in lines)
            {
                print.info(line.text);
            }
        }
    }


    
    internal static string? resolve_path(string[] directories, string stem, string caller_file, bool use_relative_path)
    {
        if(use_relative_path)
        {
            var caller = new FileInfo(caller_file).Directory?.FullName;
            if(caller != null)
            {
                var r = Path.Join(caller, stem);
                if(File.Exists(r))
                {
                    return r;
                }
            }
        }

        foreach(var dd in directories)
        {
            var r = Path.Join(dd, stem);
            if(File.Exists(r))
            {
                return r;
            }
        }

        return null;
    }


    internal static bool is_source(string path)
    {
        return Path.GetExtension(path) switch
        {
            ".cc" or ".cpp" or ".c" => true,
            _ => false
        };
    }


    
    internal static bool handle_files(Printer print, FilesArg args)
    {
        var ccpath = args.get_argument_or_none(Environment.CurrentDirectory);
        if(ccpath == null)
        {
            print.error("Failed to get compile commands");
            return false;
        }

        var commands = CompileCommands.Utils.load_compile_commands(print, ccpath);
        if(commands == null)
        {
            print.error("Failed to load compile commands");
            return false;
        }

        var walker = new FileWalker(commands);

        var file_cache = new Dictionary<string, List<Statement>>();

        foreach(var file in args.sources)
        {
            if(File.Exists(file))
            {
                if( false == walker.walk(print, file, file_cache))
                {
                    return false;
                }
            }
            else
            {
                var f = new DirectoryInfo(file).FullName;
                if(false == walker.walk(print, f, file_cache))
                {
                    return false;
                }
            }
        }

        var stats = walker.stats;

        print.info($"Top {args.count} includes are:");

        foreach(var (file, count) in stats.includes.MostCommon().Take(args.count))
        {
            var d = Path.GetRelativePath(Environment.CurrentDirectory, file);
            var times = (double)count / (double)stats.file_count;
            print.info($" - {d} {times:.2}x ({count}/{stats.file_count})");
        }

        return true;
    }
}
