using System.Collections.Immutable;
using Spectre.Console;
using Workbench.Shared;

namespace Workbench.Commands.Headers;


// todo(Gustav):
//     find out who is including XXX.h and how it's included
//     generate a union graphviz file that is a union of all includes with the TU as the root


public class Includes
{
    private static string CalculateIdentifier(string file, string? name)
    {
        return name ?? file.Replace("/", "_").Replace(".", "_").Replace("-", "_").Trim('_');
    }


    private static string CalculateDisplay(string file, string? name, string root)
    {
        return name ?? Path.GetRelativePath(root, file);
    }


    private static string GetGroup(string relative_file)
    {
        var b = relative_file.Split("/");
        if (b.Length == 1)
        {
            return "";
        }
        var r = (new[] { "libs", "external" }).Contains(b[0]) ? b[1] : b[0];
        if (r == "..")
        {
            return "";
        }
        return r;
    }

    // todo(Gustav): merge with global Graphviz
    class Graphvizer
    {
        private readonly Dictionary<string, string> nodes = new(); // id -> name
        private readonly ColCounter<string> links = new(); // link with counts

        private record GroupedItems(string Group, KeyValuePair<string, string>[] Items);

        public IEnumerable<string> GetLines(bool group, bool is_cluster)
        {
            yield return "digraph G";
            yield return "{";
            var grouped = group
                ? nodes.GroupBy(x => GetGroup(x.Value),
                        (group_name, items) => new GroupedItems(group_name, items.ToArray()))
                    .ToArray()
                : new GroupedItems[] { new("", nodes.ToArray()) }
                ;
            foreach (var (group_title, items) in grouped)
            {
                var has_group = group_title != "";
                var indent = has_group ? "    " : "";

                if (has_group)
                {
                    var cluster_prefix = is_cluster ? "cluster_" : "";
                    yield return $"subgraph {cluster_prefix}{group_title}";
                    yield return "{";
                }

                foreach (var (identifier, name) in items)
                {
                    yield return $"{indent}{identifier} [label=\"{name}\"];";
                }

                if (has_group)
                {
                    yield return "}";
                }
            }
            yield return "";
            foreach (var (code, count) in links.Items)
            {
                yield return $"{code} [label=\"{count}\"];";
            }

            yield return "}";
            yield return "";
        }

        public void Link(string source, string? name, string resolved, string root)
        {
            var from_node = CalculateIdentifier(source, name);
            var to_node = CalculateIdentifier(resolved, null);
            links.AddOne($"{from_node} -> {to_node}");

            // probably will calc more than once but who cares?
            nodes[from_node] = CalculateDisplay(source, name, root);
            nodes[to_node] = CalculateDisplay(resolved, null, root);
        }
    }

    private static string? ResolveIncludeViaIncludeDirectoriesOrNone(string include, IEnumerable<string> include_directories)
    {
        return include_directories
                .Select(directory => Path.Join(directory, include))
                .FirstOrDefault(File.Exists)
            ;
    }

    private static void gv_work(string real_file, string? name, ImmutableArray<string> include_directories, Graphvizer gv,
        ImmutableArray<string> limit, string root)
    {
        if (IsLimited(real_file, limit))
        {
            return;
        }

        foreach (var include in FindIncludeFiles(real_file))
        {
            var resolved = ResolveIncludeViaIncludeDirectoriesOrNone(include, include_directories);
            if (resolved != null)
            {
                gv.Link(real_file, name, resolved, root);
                // todo(Gustav): fix name to be based on root
                gv_work(resolved, null, include_directories, gv, limit, root);
            }
            else
            {
                gv.Link(real_file, name, include, root);
            }
        }
    }

    private static IEnumerable<string> FindIncludeFiles(string path)
    {
        var lines = File.ReadAllLines(path);
        foreach (var line in lines)
        {
            var l = line.Trim();
            // beware... shitty c++ parser
            if (l.StartsWith("#include"))
            {
                yield return l.Split(" ")[1].Trim().Trim('\"').Trim('<').Trim('>');
            }
        }
    }

    private static bool IsLimited(string real_file, IEnumerable<string> limit)
    {
        var has_limits = false;

        foreach (var l in limit)
        {
            has_limits = true;
            if (real_file.StartsWith(l))
            {
                return false;
            }
        }

        return has_limits;
    }

    private static void Work(string
            real_file, ImmutableArray<string> include_directories,
        ColCounter<string> counter, bool print_files, ImmutableArray<string> limit)
    {
        if (IsLimited(real_file, limit))
        {
            return;
        }

        foreach (var include in FindIncludeFiles(real_file))
        {
            var resolved = ResolveIncludeViaIncludeDirectoriesOrNone(include, include_directories);
            if (resolved != null)
            {
                if (print_files)
                {
                    AnsiConsole.WriteLine(resolved);
                }
                counter.AddOne(resolved);
                Work(resolved, include_directories, counter, print_files, limit);
            }
            else
            {
                if (print_files)
                {
                    AnsiConsole.WriteLine($"Unable to find {include}");
                }
                counter.AddOne(include);
            }
        }
    }

    private static IEnumerable<string> GetIncludeDirectories(string path, IReadOnlyDictionary<string, CompileCommand> cc)
    {
        var c = cc[path];
        foreach (var relative_include in c.GetRelativeIncludes())
        {
            yield return new FileInfo(Path.Join(c.Directory, relative_include)).FullName;
        }
    }


    private static IEnumerable<string> GetAllTranslationUnits(IEnumerable<string> files)
    {
        return FileUtil.SourcesFromArgs(files, FileUtil.IsSource)
                .Select(file => new FileInfo(file).FullName)
            ;
    }

    private static ImmutableArray<string> CompleteLimitArg(IEnumerable<string> args_limit)
        => args_limit.Select(x => new DirectoryInfo(x).FullName).ToImmutableArray();

    public static int HandleListIncludesCommand(Log print, string? compile_commands_arg,
        string[] args_files,
        bool args_print_files,
        bool args_print_stats,
        bool print_max,
        bool print_list,
        int args_count,
        string[] args_limit)
    {
        if (compile_commands_arg == null)
        {
            return -1;
        }
        var compile_commands = CompileCommand.LoadCompileCommandsOrNull(print, compile_commands_arg);
        if (compile_commands == null)
        {
            return -1;
        }

        var total_counter = new ColCounter<string>();
        var max_counter = new ColCounter<string>();

        var limit = CompleteLimitArg(args_limit);

        var number_of_translation_units = 0;

        foreach (var translation_unit in GetAllTranslationUnits(args_files))
        {
            number_of_translation_units += 1;
            var file_counter = new ColCounter<string>();
            if (compile_commands.ContainsKey(translation_unit))
            {
                var include_directories = GetIncludeDirectories(translation_unit, compile_commands).ToImmutableArray();
                Work(translation_unit, include_directories, file_counter, args_print_files, limit);
            }

            if (args_print_stats)
            {
                file_counter.PrintMostCommon(10);
            }
            total_counter.Update(file_counter);
            max_counter.Max(file_counter);
        }

        if (print_max)
        {
            AnsiConsole.WriteLine("");
            AnsiConsole.WriteLine("");
            AnsiConsole.WriteLine("10 top number of includes for a translation unit");
            max_counter.PrintMostCommon(10);
        }

        if (print_list)
        {
            AnsiConsole.WriteLine("");
            AnsiConsole.WriteLine("");
            AnsiConsole.WriteLine("Number of includes per translation unit");
            foreach (var (file, count) in total_counter.Items.OrderBy(x => x.Key))
            {
                if (count >= args_count)
                {
                    AnsiConsole.WriteLine($"{file} included in {count}/{number_of_translation_units}");
                }
            }
        }

        AnsiConsole.WriteLine("");
        AnsiConsole.WriteLine("");

        return 0;
    }

    public static int HandleIncludeGraphvizCommand(Log print, string? compile_commands_arg,
        string[] args_files,
        string[] args_limit,
        bool args_group,
        bool cluster_output)
    {
        if (compile_commands_arg is null)
        {
            return -1;
        }

        var compile_commands = CompileCommand.LoadCompileCommandsOrNull(print, compile_commands_arg);
        if (compile_commands == null) { return -1; }

        var limit = CompleteLimitArg(args_limit);

        var gv = new Graphvizer();

        foreach (var translation_unit in GetAllTranslationUnits(args_files))
        {
            if (compile_commands.ContainsKey(translation_unit))
            {
                var include_directories = GetIncludeDirectories(translation_unit, compile_commands).ToImmutableArray();
                gv_work(translation_unit, "TU", include_directories, gv, limit, Environment.CurrentDirectory);
            }
        }

        foreach (var line in gv.GetLines(args_group || cluster_output, cluster_output))
        {
            AnsiConsole.WriteLine(line);
        }

        return 0;
    }
}