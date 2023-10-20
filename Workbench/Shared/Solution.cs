using Spectre.Console;
using System.Collections.Immutable;
using System.Xml;
using Workbench.Shared.CMake;
using Workbench.Shared.Extensions;

namespace Workbench.Shared;

public class Solution
{
    private readonly Dictionary<Guid, Project> projects = new();

    // list always contains at least one item (unless it has been removed), missing entry means 0 items
    private readonly Dictionary<Guid, List<Guid>> uses = new();
    private readonly Dictionary<Guid, List<Guid>> is_used_by = new();

    private List<Guid> Uses(Project p) => uses.TryGetValue(p.Guid, out var ret) ? ret : new(); // app uses lib
    private List<Guid> IsUsedBy(Project p) => is_used_by.TryGetValue(p.Guid, out var ret) ? ret : new(); // lib is used by app

    public IEnumerable<Project> Projects => projects.Values;

    public enum ProjectType
    {
        Unknown, Interface, Executable, Static, Shared
    }

    public class Project
    {
        public Solution Solution { get; }
        public ProjectType Type { get; set; }
        public string Name { get; }
        public Guid Guid { get; }

        public Project(Solution solution, ProjectType type, string name, Guid guid)
        {
            Solution = solution;
            Type = type;
            Name = name;
            Guid = guid;
        }

        public override string ToString()
        {
            return $"{Name} {Type} {Guid}";
        }

        public IEnumerable<Project> Uses => Solution.Uses(this).Select(dep => Solution.projects[dep]);
        public IEnumerable<Project> IsUsedBy => Solution.IsUsedBy(this).Select(dep => Solution.projects[dep]);
    }

    public Graphviz MakeGraphviz(bool reverse)
    {
        Graphviz gv = new();

        Dictionary<string, Graphviz.Node> nodes = new();
        foreach (var p in projects.Values)
        {
            var node_shape = p.Type switch
            {
                ProjectType.Executable => Shape.Folder,
                ProjectType.Static => Shape.Component,
                ProjectType.Shared => Shape.Ellipse,
                _ => Shape.PlainText,
            };
            nodes.Add(p.Name, gv.AddNode(p.Name, node_shape));
        }

        foreach (var p in projects.Values)
        {
            foreach (var to in p.Uses)
            {
                var node_from = nodes[p.Name];
                var node_to = nodes[to.Name];
                if (reverse == false)
                {
                    gv.AddEdge(node_from, node_to);
                }
                else
                {
                    gv.AddEdge(node_to, node_from);
                }
            }
        }

        return gv;
    }

    public int RemoveProjects(Func<Project, bool> predicate)
    {
        var guids = projects.Values.Where(predicate).Select(p => p.Guid).ToImmutableHashSet();

        foreach (var g in guids)
        {
            projects.Remove(g);
            uses.Remove(g);
            is_used_by.Remove(g);

            foreach (var u in uses.Values)
            {
                u.RemoveAll(guids.Contains);
            }

            foreach (var u in is_used_by.Values)
            {
                u.RemoveAll(guids.Contains);
            }
        }

        return guids.Count;
    }

    public Project AddProject(ProjectType type, string name)
    {
        var project = new Project(this, type, name, Guid.NewGuid());
        projects.Add(project.Guid, project);
        return project;
    }

    internal void AddDependency(Project exe, Project lib)
    {
        link(exe, uses, lib);
        link(lib, is_used_by, exe);
        return;

        static void link(Project from, Dictionary<Guid, List<Guid>> store, Project to)
        {
            if (store.TryGetValue(from.Guid, out var list))
            {
                list.Add(to.Guid);
            }
            else
            {
                store.Add(from.Guid, new() { to.Guid });
            }
        }
    }


    public static class Parse
    {
        private record Dependency(Project From, string To);
        private record LoadProject(Project Project, string File);

        public static Solution CMake(IEnumerable<CMakeTrace> lines)
        {
            Solution solution = new();

            // maps name or alias to a project
            Dictionary<string, Project> name_or_alias_mapping = new();

            List<Dependency> dependencies = new();

            // load targets, aliases and list of dependencies
            foreach (var line in lines)
            {
                switch (line.Cmd.ToLower())
                {
                    case "add_executable": add_executable(line); break;
                    case "add_library": add_library(line); break;
                    case "target_link_libraries": link_library(line); break;
                }
            }

            // link dependencies to targets
            foreach (var (project, dependency_name) in dependencies)
            {
                if (name_or_alias_mapping.TryGetValue(dependency_name, out var dependency))
                {
                    solution.AddDependency(project, dependency);
                }
                else
                {
                    // todo(Gustav): print error
                }
            }

            return solution;

            void add_library(CMakeTrace line)
            {
                var lib = line.Args[0];
                AnsiConsole.MarkupLineInterpolated($"Adding lib {lib}");
                if (name_or_alias_mapping.ContainsKey(lib))
                {
                    // todo(Gustav): add warning
                    return;
                }

                if (line.Args.Contains("ALIAS"))
                {
                    var name = line.Args[2];
                    name_or_alias_mapping.Add(lib, name_or_alias_mapping[name]);
                    return;
                }

                var lib_type = ProjectType.Static;

                if (line.Args.Contains("SHARED"))
                {
                    lib_type = ProjectType.Shared;
                }

                if (line.Args.Contains("INTERFACE"))
                {
                    lib_type = ProjectType.Interface;
                }

                var p = solution.AddProject(lib_type, lib);
                name_or_alias_mapping.Add(lib, p);
            }

            void link_library(CMakeTrace line)
            {
                var target_name = line.Args[0];

                if (name_or_alias_mapping.TryGetValue(target_name, out var target) == false)
                {
                    // todo(Gustav): add warning
                    return;
                }

                foreach (var c in line.Args.Skip(1))
                {
                    if (c is "PUBLIC" or "INTERFACE" or "PRIVATE") { continue; }
                    dependencies.Add(new(target, c));
                }
            }

            void add_executable(CMakeTrace line)
            {
                var app = line.Args[0];
                var p = solution.AddProject(ProjectType.Executable, app);
                name_or_alias_mapping.Add(app, p);
            }
        }



        public static Solution VisualStudio(Log log, string solution_path)
        {
            var solution = new Solution();

            Dictionary<string, Project> projects = new();
            List<Dependency> dependencies = new();
            List<LoadProject> loads = new();

            ///////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // load solution

            {
                var lines = File.ReadLines(solution_path).ToArray();
                Project? current_project = null;
                var project_line = string.Empty;
                var solution_dir = new FileInfo(solution_path).Directory?.FullName!;
                foreach (var line in lines)
                {
                    if (line.StartsWith("Project"))
                    {
                        // this might be a "folder" or a actual project
                        project_line = line;
                    }
                    else if (line.Trim() == "ProjectSection(ProjectDependencies) = postProject")
                    {
                        // it is a project
                        var equal_index = project_line.IndexOf('=');
                        var data = project_line.Substring(equal_index + 1).Split(',', StringSplitOptions.RemoveEmptyEntries);
                        var name = data[0].Trim().Trim('\"').Trim();
                        var relative_path = data[1].Trim().Trim('\"').Trim();
                        var project_guid = data[2].Trim().Trim('\"').Trim();
                        // todo(Gustav): reuse guid?
                        current_project = solution.AddProject(ProjectType.Unknown, name);
                        loads.Add(new(current_project, Path.Join(solution_dir, relative_path)));
                        projects[project_guid.ToLowerInvariant()] = current_project;
                    }
                    else if (current_project != null && line.Trim().StartsWith("{"))
                    {
                        var project_guid = line.Split('=')[0].Trim();
                        dependencies.Add(new(current_project, project_guid));
                    }
                    else if (line == "EndProject")
                    {
                        current_project = null;
                        project_line = string.Empty;
                    }
                }
            }

            foreach (var (project, project_relative_path) in loads)
            {
                var path_to_project_file = determine_real_filename(project_relative_path);
                if (path_to_project_file == "")
                {
                    continue;
                }
                if (File.Exists(path_to_project_file) == false)
                {
                    AnsiConsole.WriteLine($"Unable to open project file: {path_to_project_file}");
                    continue;
                }
                var document = new XmlDocument();
                document.Load(path_to_project_file);
                var root_element = document.DocumentElement;
                if (root_element == null) { log.Error($"Failed to load {project_relative_path}"); continue; }
                HashSet<string> configurations = new();
                foreach (var n in root_element.ElementsNamed("VisualStudioProject").ElementsNamed("Configurations").ElementsNamed("Configuration"))
                {
                    var configuration_type = n.Attributes["ConfigurationType"];
                    if (configuration_type != null)
                    {
                        configurations.Add(configuration_type.Value);
                    }
                }

                if (configurations.Count != 0)
                {
                    var suggested_type = configurations.FirstOrDefault() ?? "";
                    if (suggested_type == "2")
                    {
                        project.Type = ProjectType.Shared;
                    }
                    else if (suggested_type == "4")
                    {
                        project.Type = ProjectType.Static;
                    }
                    else if (suggested_type == "1")
                    {
                        project.Type = ProjectType.Executable;
                    }
                }

                foreach (var n in root_element.ElementsNamed("PropertyGroup").ElementsNamed("OutputType"))
                {
                    var inner_text = n.InnerText.Trim().ToLowerInvariant();
                    if (inner_text == "winexe")
                    {
                        project.Type = ProjectType.Executable;
                    }
                    else if (inner_text == "exe")
                    {
                        project.Type = ProjectType.Executable;
                    }
                    else if (inner_text == "library")
                    {
                        project.Type = ProjectType.Shared;
                    }
                    else
                    {
                        AnsiConsole.WriteLine($"Unknown build type in {path_to_project_file}: {inner_text}");
                    }
                }

                var configuration_types = root_element.ElementsNamed("PropertyGroup").ElementsNamed("ConfigurationType").ToImmutableHashSet();
                foreach (var n in configuration_types)
                {
                    var inner_text = n.InnerText.Trim().ToLowerInvariant();
                    if (inner_text == "utility")
                    {
                    }
                    else if (inner_text == "staticlibrary")
                    {
                        project.Type = ProjectType.Static;
                    }
                    else if (inner_text == "application")
                    {
                        project.Type = ProjectType.Executable;
                    }
                    else
                    {
                        AnsiConsole.WriteLine($"Unknown build type in {path_to_project_file}: {inner_text}");
                    }
                }

                foreach (var n in root_element.ElementsNamed("ItemGroup").ElementsNamed("ProjectReference").ElementsNamed("Project"))
                {
                    var inner_text = n.InnerText.Trim();
                    dependencies.Add(new(project, inner_text));
                }
            }


            ///////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // complete loading
            foreach (var (from, dependency) in dependencies)
            {
                var project_guid = dependency.ToLowerInvariant();
                if (projects.TryGetValue(project_guid, out var project))
                {
                    solution.AddDependency(from, project);
                }
                else
                {
                    // todo(Gustav) look into these warnings...!
                    // printer.Info("Missing reference ", s)
                    // pass
                }
            }

            return solution;

            ///////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // load additional project data from file
            static string determine_real_filename(string pa)
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
        }

    }

}
