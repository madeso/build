using Spectre.Console;
using System.Collections.Immutable;

namespace Workbench.Tools;


// todo(Gustav):
// include:
//     find out who is including XXX.h and how it's included
//     generate a union graphviz file that is a union of all includes with the TU as the root


internal static class F
{
    private static IEnumerable<string> get_include_directories(string path, Dictionary<string, CompileCommands.CompileCommand> cc) {
        var c = cc[path];
        foreach(var relative_include in c.GetRelativeIncludes()) {
            yield return new FileInfo(Path.Join(c.directory, relative_include)).FullName;
        }
    }


    private static IEnumerable<string> FindIncludeFiles(string path ){
        var lines = File.ReadAllLines(path);
        foreach(var line in lines) {
            var l = line.Trim();
            // beware... shitty c++ parser
            if(l.StartsWith("#include")) {
                yield return l.Split(" ")[1].Trim().Trim('\"').Trim('<').Trim('>');
            }
        }
    }


    private static string? resolve_include_via_include_directories_or_none(string include, IEnumerable<string> include_directories){
        foreach(var directory in include_directories) {
            var joined = Path.Join(directory, include);
            if(File.Exists(joined)) {
                return joined;
            }
        }
        return null;
    }


    private static bool is_limited(string real_file, IEnumerable<string> limit){
        var hasLimits = false;

        foreach(var l in limit) {
            hasLimits = true;
            if(real_file.StartsWith(l)) {
                return false;
            }
        }

        return hasLimits;
    }


    private static string calculate_identifier(string file, string? name) {
        return name ?? file.Replace("/", "_").Replace(".", "_").Replace("-", "_").Trim('_');
    }


    private static string calculate_display(string file, string? name, string root) {
        return name ?? Path.GetRelativePath(root, file);
    }


    private static string get_group(string relative_file) {
        var b = relative_file.Split("/");
        if (b.Length == 1) {
            return "";
        }
        var r = (new string[] {"libs", "external"}).Contains(b[0]) ? b[1] : b[0];
        if(r == "..") {
            return "";
        }
        return r;
    }

    // todo(Gustav): merge with global Graphviz
    class Graphvizer {
        Dictionary<string, string> nodes = new(); // id -> name
        ColCounter<string> links = new(); // link with counts

        record GroupedItems(string Group, KeyValuePair<string, string>[] Items);

        public IEnumerable<string> print_result(bool group, bool is_cluster) {
            yield return "digraph G";
            yield return "{";
            var grouped = group
                ? this.nodes.GroupBy(x => get_group(x.Value), (group, items) => new GroupedItems(group, items.ToArray())).ToArray()
                : new GroupedItems[] { new("", this.nodes.ToArray()) }
                ;
            foreach(var (group_title, items) in grouped) {
                var has_group = group_title != "";
                var indent = has_group ? "    " : "";

                if(has_group) {
                    var cluster_prefix = is_cluster ? "cluster_" : "";
                    yield return $"subgraph {cluster_prefix}{group_title}";
                    yield return "{";
                }

                foreach(var (identifier, name) in items) {
                    yield return $"{indent}{identifier} [label=\"{name}\"];";
                }

                if(has_group) {
                    yield return "}";
                }
            }
            yield return "";
            foreach(var (code, count) in this.links.Items) {
                yield return $"{code} [label=\"{count}\"];";
            }

            yield return "}";
            yield return "";
        }

        public void link(string source, string? name, string resolved, string root){
            var from_node = calculate_identifier(source, name);
            var to_node = calculate_identifier(resolved, null);
            this.links.AddOne($"{from_node} -> {to_node}");

            // probably will calc more than once but who cares?
            this.nodes[from_node] = calculate_display(source, name, root);
            this.nodes[to_node] = calculate_display(resolved, null, root);
        }
    }


    private static void gv_work(string real_file, string? name, ImmutableArray<string> include_directories, Graphvizer gv,
        ImmutableArray<string> limit, string root)
    {
        if(is_limited(real_file, limit)) {
            return;
        }

        foreach(var include in FindIncludeFiles(real_file)) {
            var resolved = resolve_include_via_include_directories_or_none(include, include_directories);
            if(resolved != null) {
                gv.link(real_file, name, resolved, root);
                // todo(Gustav): fix name to be based on root
                gv_work(resolved, null, include_directories, gv, limit, root);
            }
            else {
                gv.link(real_file, name, include, root);
            }
        }
    }


    private static void work(Printer print, string real_file, ImmutableArray<string> include_directories, ColCounter<string> counter,
        bool print_files, ImmutableArray<string> limit)
    {
        if(is_limited(real_file, limit)) {
            return;
        }

        foreach(var include in FindIncludeFiles(real_file)) {
            var resolved = resolve_include_via_include_directories_or_none(include, include_directories);
            if(resolved != null)
            {
                if(print_files) {
                    print.Info(resolved);
                }
                counter.AddOne(resolved);
                work(print, resolved, include_directories, counter, print_files, limit);
            }
            else {
                if(print_files) {
                    print.Info($"Unable to find {include}");
                }
                counter.AddOne(include);
            }
        }
    }


    private static void print_most_common(Printer print, ColCounter<string> counter, int ncount)
    {
        foreach(var (file, count) in counter.MostCommon().Take(ncount))
        {
            print.Info($"{file}: {count}");
        }
    }


    private static IEnumerable<string> all_translation_units(IEnumerable<string> files) {
        foreach(var patt in files)
        {
            foreach(var file in FileUtil.ListFilesRecursivly(patt, FileUtil.SOURCE_FILES))
            {
                yield return new FileInfo(file).FullName;
            }
        }
    }


    private static int get_line_count(string path, bool discard_empty)
    {
        var lines = File.ReadLines(path);
        int count = 0;
        foreach(var line in lines)
        {
            if(discard_empty && string.IsNullOrWhiteSpace(line))
            {
                // pass
            }
            else
            {
                count += 1;
            }
        }
        return count;
    }


    private static IEnumerable<int> get_all_indent(string path , bool discard_empty) {
        var lines = File.ReadLines(path);
        foreach (var line in lines)
        {
            if (discard_empty && string.IsNullOrWhiteSpace(line))
            {
                // pass
            }
            else
            {
                yield return line.Length - line.TrimStart().Length;
            }
        }
    }


    private static int get_max_indent(string path, bool discard_empty) {
        int count = 0;
        var got_files = false;

        foreach(var line_count in get_all_indent(path, discard_empty))
        {
            count = Math.Max(count, line_count);
            got_files = true;
        }
        
        return got_files ? count : -1;
    }


    private static bool contains_pragma_once(string path) {
        return File.ReadLines(path)
            .Where(line => line.Contains("#pragma once"))
            .Any();
    }


    private static bool file_is_in_folder(string file, string folder)
    {
        return new FileInfo(file).FullName.StartsWith(new DirectoryInfo(folder).FullName);
    }



    ///////////////////////////////////////////////////////////////////////////
    //// handlers

    public static int handle_missing_include_guards(Printer print, string[] argfiles)
    {
        var count = 0;
        var files = 0;

        foreach(var patt in argfiles)
        {
            foreach(var file in FileUtil.ListFilesRecursivly(patt, FileUtil.HEADER_FILES))
            {
                files += 1;
                if(contains_pragma_once(file) == false)
                {
                    print.Info(file);
                    count += 1;
                }
            }
        }

        print.Info($"Found {count} in {files} files.");
        return 0;
    }


    private static string float_to_block_str(float f)
    {
        var block_width_char = new[] { "▏", "▎", "▍", "▌", "▋", "▊", "▉", "█" };
        var size = block_width_char.Length;
        var count = Math.Max((int)(f * size), 1);
        string r = "";
        while(count > 0)
        {
            var bi = count >= block_width_char.Length
                ? block_width_char.Length-1
                : count
                ;
            r += block_width_char[bi];
            count -= size;
        }
        return r;
    }


    public static int handle_list_indents(Printer print, string[] argsFiles, int each, bool argsShow, bool argsHist, bool discardEmpty)
    {
        var stats = new Dictionary<int, List<string>>();
        var foundFiles = 0;

        foreach(var patt in argsFiles) {
            foreach (var file in FileUtil.ListFilesRecursivly(patt, FileUtil.HEADER_AND_SOURCE_FILES))
            {
                foundFiles += 1;

                var counts = !argsHist
                    ? new int[] { get_max_indent(file, discardEmpty) }
                    : get_all_indent(file, discardEmpty)
                        .Order()
                        .Distinct()
                        .ToArray()
                    ;

                foreach(var count in counts) {
                    var index = each <= 1
                        ? count
                        : count - (count % each)
                        ;
                    if(stats.TryGetValue(index, out var values)) {
                        values.Add(file);
                    }
                    else {
                        stats.Add(index, new List<string> { file });
                    }
                }
            }
        }

        var all_sorted = stats.OrderBy(x => x.Key).ToImmutableArray();
        print.Info($"Found {foundFiles} files.");
        
        var total_sum = argsHist
            ? all_sorted.Select(x => x.Value.Count).Max()
            : 0
            ;
        
        foreach(var (count,files) in all_sorted) {
            var c = files.Count;
            var count_str = each <= 1
                ? $"{count}"
                : $"{count}-{count+each-1}"
                ;
            if(argsHist)
            {
                var hist_width = 50;
                var chars = float_to_block_str((c * hist_width)/total_sum);
                print.Info($"{count_str:<6}: {chars}");
            }
            else if(argsShow && c < 3)
            {
                print.Info($"{count_str}: {files}");
            }
            else
            {
                print.Info($"{count_str}: {c}");
            }
        }

        return 0;
    }

    private static ImmutableArray<string> complete_limit_arg(string[] args_limit)
    {
        return args_limit.Select(x => new DirectoryInfo(x).FullName).ToImmutableArray();
    }

    public static int handle_list(Printer print, string? compile_commands_arg,
        string[] args_files,
        bool args_print_files,
        bool args_print_stats,
        bool args_print_max,
        bool args_print_list,
        int args_count,
        string[] args_limit)
    {
        if(compile_commands_arg == null) {
            return -1;
        }
        var compile_commands = CompileCommands.Utils.LoadCompileCommandsOrNull(print, compile_commands_arg);
        if(compile_commands == null)
        {
            return -1;
        }

        var total_counter = new ColCounter<string>();
        var max_counter = new ColCounter<string>();

        var limit = complete_limit_arg(args_limit);

        int files = 0;

        foreach(var real_file in all_translation_units(args_files)) {
            files += 1;
            var file_counter = new ColCounter<string>();
            if(compile_commands.ContainsKey(real_file))
            {
                var include_directories = get_include_directories(real_file, compile_commands).ToImmutableArray();
                work(print, real_file, include_directories, file_counter, args_print_files, limit);
            }

            if(args_print_stats) {
                print_most_common(print, file_counter, 10);
            }
            total_counter.update(file_counter);
            max_counter.Max(file_counter);
        }

        if(args_print_max) {
            print.Info("");
            print.Info("");
            print.Info("10 top number of includes for a translation unit");
            print_most_common(print, max_counter, 10);
        }

        if(args_print_list) {
            print.Info("");
            print.Info("");
            print.Info("Number of includes per translation unit");
            foreach(var (file, count) in total_counter.Items.OrderBy(x=>x.Key))
            {
                if(count>= args_count)
                {
                    print.Info($"{file} included in {count}/{files}");
                }
            }
        }

        print.Info("");
        print.Info("");

        return 0;
    }

    public static int handle_gv(Printer print, string? compile_commands_arg,
        string[] args_files,
        string[] args_limit,
        bool args_group,
        bool args_cluster)
    {
        if(compile_commands_arg is null)
        {
            return -1;
        }

        var compile_commands = CompileCommands.Utils.LoadCompileCommandsOrNull(print, compile_commands_arg);
        if(compile_commands == null) { return -1; }

        var limit = complete_limit_arg(args_limit);

        var gv = new Graphvizer();

        foreach(var real_file in all_translation_units(args_files))
        {
            if(compile_commands.ContainsKey(real_file))
            {
                var include_directories = get_include_directories(real_file, compile_commands).ToImmutableArray();
                gv_work(real_file, "TU", include_directories, gv, limit, Environment.CurrentDirectory);
            }
        }

        foreach(var line in gv.print_result(args_group || args_cluster, args_cluster))
        {
            print.Info(line);
        }

        return 0;
    }

    public static int handle_list_no_project_folder(Printer print, string[] args_files, string? compile_commands_arg){
        if (compile_commands_arg == null) { return -1; }

        var bases = args_files.Select(x => FileUtil.RealPath(x)).ToImmutableArray();

        var build_root = new FileInfo(compile_commands_arg).Directory?.FullName;
        if(build_root == null) { return -1; }

        var projects = new HashSet<string>();
        var projects_with_folders = new HashSet<string>();
        var files = new Dictionary<string, string>();
        var project_folders = new ColCounter<string>();

        foreach(var cmd in CMake.Trace.TraceDirectory(build_root))
        {
            if(bases.Select(b => file_is_in_folder(cmd.File, b)).Any())
            {
                if((new string[] {"add_library", "add_executable"}).Contains(cmd.Cmd.ToLowerInvariant()))
                {
                    var project_name = cmd.Args[0];
                    if(cmd.Args[1] != "INTERFACE") { // skip interface libraries
                        projects.Add(project_name);
                        files[project_name] = cmd.File;
                    }
                }
                if(cmd.Cmd.ToLowerInvariant() == "set_target_properties")
                {
                    // set_target_properties(core PROPERTIES FOLDER "Libraries")
                    if(cmd.Args[1] == "PROPERTIES" && cmd.Args[2] == "FOLDER")
                    {
                        projects_with_folders.Add(cmd.Args[0]);
                        project_folders.AddOne(cmd.Args[3]);
                    }
                }
            }
        }

        // var sort_on_cmake = lambda x: x[1];
        var missing = projects
            .Where(x => projects_with_folders.Contains(x) == false)
            .Select(m => new { Missing=m, File = files[m] })
            .OrderBy(x=>x.File)
            .ToImmutableArray();
        var total_missing = missing.Length;

        int missing_files = 0;

        var grouped = missing.GroupBy(x => x.File, (g, list) => new { cmake_file = g, sorted_files = list.Select(x=>x.Missing).OrderBy(x=>x).ToImmutableArray() });
        foreach (var g in grouped)
        {
            missing_files += 1;
            print.Info(Path.GetRelativePath(Environment.CurrentDirectory, g.cmake_file));
            foreach(var f in g.sorted_files) {
                print.Info($"    {f}");
            }
            if(g.sorted_files.Length > 1) {
                print.Info($"    = {g.sorted_files.Length} projects");
            }
            print.Info("");
        }
        print.Info($"Found missing: {total_missing} projects in {missing_files} files");
        print_most_common(print, project_folders, 10);

        return 0;
    }


    public static int handle_missing_in_cmake(Printer print, string[] args_files, string? compile_commands_arg)
    {
        if (compile_commands_arg == null) { return -1; }

        var bases = args_files.Select(x => FileUtil.RealPath(x)).ToImmutableArray();

        var build_root = new FileInfo(compile_commands_arg).Directory?.FullName;
        if (build_root == null) { return -1; }

        var paths = new HashSet<string>();

        foreach (var cmd in CMake.Trace.TraceDirectory(build_root))
        {
            if (bases.Select(b => file_is_in_folder(cmd.File, b)).Any())
            {
                if(cmd.Cmd.ToLower() == "add_library")
                {
                    paths.UnionWith(cmd.ListFilesInCmakeLibrary());
                }
                if(cmd.Cmd.ToLower() == "add_executable")
                {
                    paths.UnionWith(cmd.ListFilesInCmakeLibrary());
                }
            }
        }

        var count = 0;
        foreach(var patt in args_files)
        {
            foreach(var file in FileUtil.ListFilesRecursivly(patt, FileUtil.HEADER_AND_SOURCE_FILES))
            {
                var resolved = new FileInfo(file).FullName;
                if(paths.Contains(resolved) == false)
                {
                    print.Info(resolved);
                    count += 1;
                }
            }
        }

        print.Info($"Found {count} files not referenced in cmake");
        return 0;
    }

    public static int handle_line_count(Printer print, string[] args_files,
        int each,
        bool args_show,
        bool args_discard_empty)
    {
        var stats = new Dictionary<int, List<string>>();
        var fileCount = 0;

        foreach(var patt in args_files)
        {
            foreach(var file in FileUtil.ListFilesRecursivly(patt, FileUtil.HEADER_AND_SOURCE_FILES))
            {
                fileCount += 1;

                var count = get_line_count(file, args_discard_empty);

                var index = each <= 1 ? count : count - (count % each);
                if(stats.TryGetValue(index, out var datavalues))
                {
                    datavalues.Add(file);
                }
                else {
                    stats.Add(index, new List<string>{ file });
                }
            }
        }

        print.Info($"Found {fileCount} files.");
        foreach(var (count, files) in stats.OrderBy(x=>x.Key))
        {
            var c = files.Count;
            var count_str = each <= 1 ? $"{count}" : $"{count}-{count+each-1}";
            if(args_show && c < 3) {
                print.Info($"{count_str}: {files}");
            }
            else {
                print.Info($"{count_str}: {c}");
            }
        }

        return 0;
    }


    public static int handle_check_files(Printer print, string[] args_files)
    {
        var files = 0;
        var errors = 0;

        foreach(var patt in args_files)
        {
            foreach (var file in FileUtil.ListFilesRecursivly(patt, FileUtil.HEADER_AND_SOURCE_FILES))
            {
                files += 1;
                if(file.Contains('-'))
                {
                    errors +=1;
                    print.Error($"file name mismatch: {file}");
                }
            }
        }

        print.Info($"Found {errors} errors in {files} files.");
        
        return errors > 0 ? -1 : 0;
    }
}
