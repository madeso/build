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


    private static string CalculateDisplay(string file, string? name, Dir root)
    {
        return name ?? Path.GetRelativePath(root.Path, file);
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

        public void Link(Fil source, string? name, string resolved, Dir root)
        {
            var from_node = CalculateIdentifier(source.Path, name);
            var to_node = CalculateIdentifier(resolved, null);
            links.AddOne($"{from_node} -> {to_node}");

            // probably will calc more than once but who cares?
            nodes[from_node] = CalculateDisplay(source.Path, name, root);
            nodes[to_node] = CalculateDisplay(resolved, null, root);
        }
    }

    private static Fil? ResolveIncludeViaIncludeDirectoriesOrNone(string include, IEnumerable<Dir> include_directories)
    {
        return include_directories
                .Select(directory => directory.GetFile(include))
                .FirstOrDefault(f => f.Exists)
            ;
    }

    private static void gv_work(Vfs vfs, Fil real_file, string? name, ImmutableArray<Dir> include_directories,
        Graphvizer gv, ImmutableArray<Dir> limit, Dir root)
    {
        if (IsLimited(real_file, limit))
        {
            return;
        }

        foreach (var include in FindIncludeFiles(vfs, real_file))
        {
            var resolved = ResolveIncludeViaIncludeDirectoriesOrNone(include, include_directories);
            if (resolved != null)
            {
                gv.Link(real_file, name, resolved.Path, root);
                // todo(Gustav): fix name to be based on root
                gv_work(vfs, resolved, null, include_directories, gv, limit, root);
            }
            else
            {
                gv.Link(real_file, name, include, root);
            }
        }
    }

    private static IEnumerable<string> FindIncludeFiles(Vfs vfs, Fil path)
    {
        var lines = path.ReadAllLines(vfs);
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

    private static bool IsLimited(Fil real_file, IEnumerable<Dir> limit)
    {
        var has_limits = false;

        foreach (var l in limit)
        {
            has_limits = true;
            if (l.HasFile(real_file))
            {
                return false;
            }
        }

        return has_limits;
    }

    private static void Work(Vfs vfs, Dir cwd, Fil real_file, ImmutableArray<Dir> include_directories,
        ColCounter<string> counter, bool print_files, ImmutableArray<Dir> limit)
    {
        if (IsLimited(real_file, limit))
        {
            return;
        }

        foreach (var include in FindIncludeFiles(vfs, real_file))
        {
            var resolved = ResolveIncludeViaIncludeDirectoriesOrNone(include, include_directories);
            if (resolved != null)
            {
                if (print_files)
                {
                    AnsiConsole.WriteLine(resolved.GetDisplay(cwd));
                }
                counter.AddOne(resolved.Path);
                Work(vfs, cwd, resolved, include_directories, counter, print_files, limit);
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

    private static IEnumerable<Dir> GetIncludeDirectories(Fil path,
        IReadOnlyDictionary<Fil, CompileCommand> cc)
        => cc[path].GetRelativeIncludes();


    private static IEnumerable<Fil> GetAllTranslationUnits(Dir cwd, IEnumerable<string> files)
        => FileUtil.SourcesFromArgs(cwd, files, FileUtil.IsSource);

    private static ImmutableArray<Dir> CompleteLimitArg(Dir cwd, Log log, IEnumerable<string> args_limit)
        => Cli.ToDirectories(cwd, log, args_limit).ToImmutableArray();

    public static int HandleListIncludesCommand(Vfs vfs, Dir cwd, Log print, Fil? compile_commands_arg,
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
        var compile_commands = CompileCommand.LoadCompileCommandsOrNull(vfs, print, compile_commands_arg);
        if (compile_commands == null)
        {
            return -1;
        }

        var total_counter = new ColCounter<string>();
        var max_counter = new ColCounter<string>();

        var limit = CompleteLimitArg(cwd, print, args_limit);

        var number_of_translation_units = 0;

        foreach (var translation_unit in GetAllTranslationUnits(cwd, args_files))
        {
            number_of_translation_units += 1;
            var file_counter = new ColCounter<string>();
            if (compile_commands.ContainsKey(translation_unit))
            {
                var include_directories = GetIncludeDirectories(translation_unit, compile_commands).ToImmutableArray();
                Work(vfs, cwd, translation_unit, include_directories, file_counter, args_print_files, limit);
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

    public static int HandleIncludeGraphvizCommand(Vfs vfs, Dir cwd, Log print, Fil? compile_commands_arg,
        string[] args_files,
        string[] args_limit,
        bool args_group,
        bool cluster_output)
    {
        if (compile_commands_arg is null)
        {
            return -1;
        }

        var compile_commands = CompileCommand.LoadCompileCommandsOrNull(vfs, print, compile_commands_arg);
        if (compile_commands == null) { return -1; }

        var limit = CompleteLimitArg(cwd, print, args_limit);

        var gv = new Graphvizer();

        foreach (var translation_unit in GetAllTranslationUnits(cwd, args_files))
        {
            if (compile_commands.ContainsKey(translation_unit))
            {
                var include_directories = GetIncludeDirectories(translation_unit, compile_commands).ToImmutableArray();
                gv_work(vfs, translation_unit, "TU", include_directories, gv, limit, cwd);
            }
        }

        foreach (var line in gv.GetLines(args_group || cluster_output, cluster_output))
        {
            AnsiConsole.WriteLine(line);
        }

        return 0;
    }
}