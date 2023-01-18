using Spectre.Console;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using System.Xml;

namespace Workbench.SlnDeps;

/*
import argparse
import os
import os.path
import xml.etree.ElementTree as ET
from enum import Enum
import re
import os
import subprocess
import typing
*/

static class F
{

    // ======================================================================================================================
    // project
    // ======================================================================================================================

    enum Build
    {
        Unknown,
        Application,
        Static,
        Shared,
    }


    private static string DetermineRealFilename(string pa)
    {
        var p = pa;
        if (File.Exists(p))
        {
            return p;
        }
        p = pa + ".vcxproj";
        if (File.Exists(p))
        {
            return p;
        }
        p = pa + ".csproj";
        if (File.Exists(p))
        {
            return p;
        }
        return "";
    }


    class Project
    {
        public Build Type { get; private set; } = Build.Unknown;
        public List<Project> dependencies { get; } = new();
        public HashSet<string> named_dependencies { get; set; } = new();

        public string display_name { get; }
        public string path { get; }
        public string guid { get; }

        public Project(string name, string path, string guid)
        {
            this.display_name = name;
            this.path = path;
            this.guid = guid;
        }

        public string get_safe_name()
        {
            return this.display_name.Replace(" ", "_").Replace("-", "_").Replace(".", "_");
        }

        public override string ToString()
        {
            return this.get_safe_name();
        }

        public void resolve(Dictionary<string, Project> projects)
        {
            // printer.Info("Resolving ", this.Name, len(this.sdeps))
            foreach (var dependency in this.named_dependencies)
            {
                var s = dependency.ToLowerInvariant();
                if (projects.TryGetValue(s, out var pp))
                {
                    this.dependencies.Add(pp);
                }
                else
                {
                    // todo(Gustav) look into theese warnings...!
                    // printer.Info("Missing reference ", s)
                    // pass
                }
            }
        }

        public void load_information(Printer printer)
        {
            var p = DetermineRealFilename(this.path);
            if (p == "")
            {
                return;
            }
            if (File.Exists(p) == false)
            {
                printer.Info($"Unable to open project file: {p}");
                return;
            }
            var document = new XmlDocument();
            document.Load(p);
            var root_element = document.DocumentElement;
            if (root_element == null) { printer.Error($"Failed to load {this.path}"); return; }
            var namespace_match = Regex.Match("\\{.*\\}", root_element.Name);
            var xmlNamespace = namespace_match.Success ? namespace_match.Groups[0].Value : "";
            HashSet<string> configurations = new();
            foreach (var n in root_element.FindAll($"{xmlNamespace}VisualStudioProject/{xmlNamespace}Configurations/{xmlNamespace}Configuration[@ConfigurationType]"))
            {
                if (n.Attributes == null) { continue; }
                var configuration_type = n.Attributes["ConfigurationType"];
                if (configuration_type != null)
                {
                    configurations.Add(configuration_type.Value);
                }
            }
            this.Type = Build.Unknown;

            if (configurations.Count != 0)
            {
                var suggested_type = configurations.FirstOrDefault() ?? "";
                if (suggested_type == "2")
                {
                    this.Type = Build.Shared;
                }
                else if (suggested_type == "4")
                {
                    this.Type = Build.Static;
                }
                else if (suggested_type == "1")
                {
                    this.Type = Build.Application;
                }
            }
            foreach (var n in root_element.FindAll($"./{xmlNamespace}PropertyGroup/{xmlNamespace}OutputType"))
            {
                var inner_text = n.InnerText.Trim().ToLowerInvariant();
                if (inner_text == "winexe")
                {
                    this.Type = Build.Application;
                }
                else if (inner_text == "exe")
                {
                    this.Type = Build.Application;
                }
                else if (inner_text == "library")
                {
                    this.Type = Build.Shared;
                }
                else
                {
                    printer.Info($"Unknown build type in {p}: {inner_text}");
                }
            }
            foreach (var n in root_element.FindAll($"./{xmlNamespace}ItemGroup/{xmlNamespace}ProjectReference/{xmlNamespace}Project"))
            {
                var inner_text = n.InnerText.Trim();
                this.add_named_dependency(inner_text);
            }
        }

        public void add_named_dependency(string dep)
        {
            this.named_dependencies.Add(dep.ToLowerInvariant());
        }
    }


    // ======================================================================================================================
    // solution
    // ======================================================================================================================

    class Solution
    {
        public Dictionary<string, Project> projects { get; } = new();
        string Name = "";

        public Solution(Printer printer, string solution_path)
        {
            this.Name = Path.GetFileNameWithoutExtension(solution_path);
            var lines = File.ReadLines(solution_path);
            Project? current_project = null;
            Project? dependency_project = null;
            var solutionDir = new FileInfo(solution_path).Directory?.FullName!;
            foreach (var line in lines)
            {
                if (line.StartsWith("Project"))
                {
                    var equal_index = line.IndexOf('=');
                    var data = line.Substring(equal_index + 1).Split(',', StringSplitOptions.RemoveEmptyEntries);
                    var name = data[0].Trim().Trim('\"').Trim();
                    var relative_path = data[1].Trim().Trim('\"').Trim();
                    var project_guid = data[2].Trim().Trim('\"').Trim();
                    current_project = new Project(
                        name: name,
                        path: Path.Join(solutionDir, relative_path),
                        guid: project_guid
                    );
                    this.projects[current_project.guid.ToLowerInvariant()] = current_project;
                }
                else if (line == "EndProject")
                {
                    current_project = null;
                    dependency_project = null;
                }
                else if (line.Trim() == "ProjectSection(ProjectDependencies) = postProject")
                {
                    dependency_project = current_project;
                }
                else if (dependency_project != null && line.Trim().StartsWith("{"))
                {
                    var project_guid = line.Split('=')[0].Trim();
                    dependency_project.add_named_dependency(project_guid);
                }
            }
            foreach (var p in this.projects.Values)
            {
                p.load_information(printer);
            }
            this.post_load();
        }

        public void post_load()
        {
            foreach (var p in this.projects.Values)
            {
                p.resolve(this.projects);
            }
        }


        public IEnumerable<string> generate_graphviz_source_lines(ExclusionList exclude, bool reverse_arrows)
        {
            var indent = "    ";

            var project_names_to_exclude = exclude;
            yield return "digraph " + this.Name.Replace("-", "_") + " {";
            yield return "";
            yield return indent + "/********** projects **********/";
            foreach (var project in this.projects.Values)
            {
                if (project_names_to_exclude.ShouldExclude(project.display_name))
                {
                    continue;
                }
                var decoration = "label=\"" + project.display_name + "\"";
                var shape = "plaintext";
                if (project.Type == Build.Application)
                {
                    shape = "folder";
                }
                else if (project.Type == Build.Shared)
                {
                    shape = "ellipse";
                }
                else if (project.Type == Build.Static)
                {
                    shape = "component";
                }
                decoration += ", shape=" + shape;
                yield return indent + project.get_safe_name() + " [" + decoration + "]" + ";";
            }
            yield return "";
            yield return "";
            yield return indent + "/********** dependencies **********/";
            var first_project = true;
            foreach (var project in this.projects.Values)
            {
                if (project.dependencies.Count == 0)
                {
                    // printer.Info("not enough ", project.Name)
                    continue;
                }
                if (project_names_to_exclude.ShouldExclude(project.display_name))
                {
                    continue;
                }
                if (first_project == false)
                {
                    yield return "";
                }
                else
                {
                    first_project = false;
                }
                foreach (var dependency in project.dependencies)
                {
                    if (project_names_to_exclude.ShouldExclude(dependency.display_name))
                    {
                        continue;
                    }
                    if (reverse_arrows)
                    {
                        yield return indent + dependency.get_safe_name() + " -> " + project.get_safe_name() + ";";
                    }
                    else
                    {
                        yield return indent + project.get_safe_name() + " -> " + dependency.get_safe_name() + ";";
                    }
                }
            }
            yield return "}";
        }

        public void write_graphviz(string target_file, ExclusionList exclude, bool reverse_arrows)
        {
            var lines = this.generate_graphviz_source_lines(exclude, reverse_arrows);
            File.WriteAllLines(target_file, lines);
        }

        /*
        given the dependencies like:
        a -> b
        b -> c
        a -> c
        simplify will remove the last dependency (a->c) to "simplify" the graph
        */
        public void simplify()
        {
            foreach (var project in this.projects.Values)
            {
                var dependencies = new HashSet<string>();
                foreach (var dependency_name in project.named_dependencies)
                {
                    if (this.has_dependency(project, dependency_name, false) == false)
                    {
                        dependencies.Add(dependency_name);
                    }
                }
                project.named_dependencies = dependencies;
            }
            this.post_load();
        }

        private bool has_dependency(Project project, string dependency_name, bool self_reference)
        {
            foreach (var current_name in project.named_dependencies)
            {
                if (self_reference && current_name == dependency_name)
                {
                    return true;
                }
                if (this.has_dependency(this.projects[current_name], dependency_name, true))
                {
                    return true;
                }
            }
            return false;
        }
    }

    public class ExclusionList
    {
        readonly ImmutableHashSet<string> explicits;
        readonly ImmutableArray<string> contains;

        public ExclusionList(IEnumerable<string> exclude, IEnumerable<string> contains, bool cmake)
        {
            this.explicits = Exclude(exclude, cmake).Select(name => Transform(name)).ToImmutableHashSet();
            this.contains = contains.ToImmutableArray();
        }

        private static string Transform(string name)
        {
            return name.ToLowerInvariant().Trim();
        }

        private static IEnumerable<string> Exclude(IEnumerable<string> args_exclude, bool cmake)
        {
            if (cmake)
            {
                return args_exclude.Concat(new string[] {
                        "ZERO_CHECK", "RUN_TESTS", "NightlyMemoryCheck", "ALL_BUILD",
                        "Continuous", "Experimental", "Nightly",
                    });
            }
            else
            {
                return args_exclude;
            }
        }

        internal bool ShouldExclude(string display_name)
        {
            var name = Transform(display_name);
            if (explicits.Contains(name))
            {
                return true;
            }

            if (contains.Where(n=> name.Contains(n)).Any())
            {
                return true;
            }

            return false;
        }
    }


    // ======================================================================================================================
    // logic
    // ======================================================================================================================

    private static void run_graphviz(Printer printer, string target_file, string image_format, string graphviz_layout)
    {
        var cmdline = new ProcessBuilder(
            "dot",
            target_file + ".graphviz", "-T" + image_format,
            "-K" + graphviz_layout,
            "-O" + target_file + "." + image_format
        );
        printer.Info($"Running graphviz {cmdline}");
        cmdline.RunAndPrintOutput(printer);
    }


    private static string value_or_default(string value, string def)
    {
        var vt = value.Trim();
        return vt.Trim() == "" || vt.Trim() == "?" ? def : value;
    }


    // ======================================================================================================================
    // Handlers
    // ======================================================================================================================

    public static int handle_generate(Printer printer, string args_target,
            string args_format,
            ExclusionList exl,
            bool args_simplify,
            bool args_reverse,
            string args_solution,
            string args_style)
    {
        var path_to_solution_file = args_solution;
        var exclude = exl;
        var simplify = args_simplify;
        var reverse_arrows = args_reverse;
        var graphviz_layout = args_style ?? "dot";

        var solution = new Solution(printer, path_to_solution_file);
        if (simplify)
        {
            solution.simplify();
        }

        var image_format = value_or_default(args_format, "svg");
        var target_file = value_or_default(args_target, ChangeExtension(path_to_solution_file, image_format));

        solution.write_graphviz(target_file + ".graphviz", exclude, reverse_arrows);
        run_graphviz(printer, target_file, image_format, graphviz_layout);

        return 0;
    }

    private static string ChangeExtension(string file, string new_ext)
    {
        var dir = new FileInfo(file).Directory?.FullName!;
        var name = Path.GetFileNameWithoutExtension(file);
        return Path.Join(dir, $"{name}.{new_ext}");
    }

    public static int handle_source(Printer printer, ExclusionList exclude,
            bool args_simplify,
            bool args_reverse,
            string args_solution)
    {
        var path_to_solution_file = args_solution;
        var simplify = args_simplify;
        var reverse_arrows = args_reverse;

        var solution = new Solution(printer, path_to_solution_file);
        if (simplify)
        {
            solution.simplify();
        }

        var lines = solution.generate_graphviz_source_lines(exclude, reverse_arrows);
        foreach (var line in lines)
        {
            printer.Info(line);
        }

        return 0;
    }

    public static int handle_write(Printer printer, ExclusionList exl, string args_target,
            bool args_simplify,
            bool args_reverse,
            string args_solution)
    {
        var path_to_solution_file = args_solution;
        var exclude = exl;
        var simplify = args_simplify;
        var reverse_arrows = args_reverse;

        var solution = new Solution(printer, path_to_solution_file);
        if (simplify)
        {
            solution.simplify();
        }

        var target_file = value_or_default(args_target, ChangeExtension(path_to_solution_file, "gv"));

        solution.write_graphviz(target_file, exclude, reverse_arrows);
        printer.Info($"Wrote {target_file}");

        return 0;
    }

    public static int handle_list(Printer printer, string args_solution)
    {
        var solution = new Solution(printer, args_solution);
        foreach (var project in solution.projects.Values)
        {
            printer.Info(project.display_name);
        }

        printer.Info("");

        return 0;
    }
}