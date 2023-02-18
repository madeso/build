using Spectre.Console;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml;
using Workbench.CMake;
using Workbench.Utils;
using static Workbench.Solution;

namespace Workbench;

public class Solution
{
    private readonly Dictionary<Guid, Project> projects = new();

    // list always contains atleast one item (unless it has been removed), missing entry means 0 items
    private readonly Dictionary<Guid, List<Guid>> uses = new();
    private readonly Dictionary<Guid, List<Guid>> isUsedBy = new();

    private List<Guid> Uses(Project p) => uses.TryGetValue(p.Guid, out var ret) ? ret : new(); // app uses lib
    private List<Guid> IsUsedBy(Project p) => this.isUsedBy.TryGetValue(p.Guid, out var ret) ? ret : new(); // lib is used by app

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
        foreach (var p in this.projects.Values)
        {
            var nodeShape = p.Type switch
            {
                ProjectType.Executable => "folder",
                ProjectType.Static => "component",
                ProjectType.Shared => "ellipse",
                _ => "plaintext",
            };
            nodes.Add(p.Name, gv.AddNode(p.Name, nodeShape));
        }

        foreach (var p in this.projects.Values)
        {
            foreach (var to in p.Uses)
            {
                var nfrom = nodes[p.Name];
                var nto = nodes[to.Name];
                if (reverse == false)
                {
                    gv.AddEdge(nfrom, nto);
                }
                else
                {
                    gv.AddEdge(nto, nfrom);
                }
            }
        }

        return gv;
    }

    public int RemoveProjects(Func<Project, bool> predicate)
    {
        var guids = this.projects.Values.Where(predicate).Select(p => p.Guid).ToImmutableHashSet();
        
        foreach(var g in guids)
        {
            this.projects.Remove(g);
            this.uses.Remove(g);
            this.isUsedBy.Remove(g);

            foreach(var u in uses.Values)
            {
                u.RemoveAll(guids.Contains);
            }

            foreach (var u in isUsedBy.Values)
            {
                u.RemoveAll(guids.Contains);
            }
        }

        return guids.Count;
    }

    public Project AddProject(ProjectType type, string name)
    {
        var project = new Project(this, type, name, Guid.NewGuid());
        this.projects.Add(project.Guid, project);
        return project;
    }

    internal void AddDependency(Project exe, Project lib)
    {
        static void Link(Project from, Dictionary<Guid, List<Guid>> store, Project to)
        {
            if(store.TryGetValue(from.Guid, out var list))
            {
                list.Add(to.Guid);
            }
            else
            {
                store.Add(from.Guid, new() { to.Guid });
            }
        }
        Link(exe, uses, lib);
        Link(lib, isUsedBy, exe);
    }
}

internal class SolutionParser
{
    record Dependency(Solution.Project From, string To);
    record LoadProject(Solution.Project Project, string File);

    public static Solution ParseCmake(IEnumerable<CMake.Trace> lines)
    {
        Solution solution = new();

        // maps name or alias to a project
        Dictionary<string, Solution.Project> nameOrAliasMapping = new();

        List<Dependency> dependencies = new();

        void AddExecutable(Trace line)
        {
            var app = line.Args[0];
            var p = solution.AddProject(Solution.ProjectType.Executable, app);
            nameOrAliasMapping.Add(app, p);
        }

        void AddLibrary(Trace line)
        {
            var lib = line.Args[0];
            AnsiConsole.MarkupLineInterpolated($"Adding lib {lib}");
            if (nameOrAliasMapping.ContainsKey(lib))
            {
                // todo(Gustav): add warning
                return;
            }

            if (line.Args.Contains("ALIAS"))
            {
                var name = line.Args[2];
                nameOrAliasMapping.Add(lib, nameOrAliasMapping[name]);
                return;
            }

            var libType = Solution.ProjectType.Static;

            if (line.Args.Contains("SHARED"))
            {
                libType = Solution.ProjectType.Shared;
            }

            if (line.Args.Contains("INTERFACE"))
            {
                libType = Solution.ProjectType.Interface;
            }

            var p = solution.AddProject(libType, lib);
            nameOrAliasMapping.Add(lib, p);
        }

        void LinkLibrary(Trace line)
        {
            var targetName = line.Args[0];

            if (nameOrAliasMapping.TryGetValue(targetName, out var target) == false)
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

        // load targets, aliases and list of dependencies
        foreach (var line in lines)
        {
            switch (line.Cmd.ToLower())
            {
                case "add_executable": AddExecutable(line); break;
                case "add_library": AddLibrary(line); break;
                case "target_link_libraries": LinkLibrary(line); break;
            }
        }

        // link dependencies to targets
        foreach(var (project, dependencyName) in dependencies)
        {
            if(nameOrAliasMapping.TryGetValue(dependencyName, out var dependency))
            {
                solution.AddDependency(project, dependency);
            }
            else
            {
                // todo(Gustav): print error
            }
        }

        return solution;
    }



    public static Solution ParseVisualStudio(Printer printer, string solution_path)
    {
        var solution = new Solution();

        Dictionary<string, Solution.Project> projects = new();
        List<Dependency> dependencies = new();
        List<LoadProject> loads = new();

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // load solution

        {
            var lines = File.ReadLines(solution_path).ToArray();
            Project? current_project = null;
            string project_line = string.Empty;
            var solutionDir = new FileInfo(solution_path).Directory?.FullName!;
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
                    current_project = solution.AddProject(Solution.ProjectType.Unknown, name);
                    loads.Add(new(current_project, Path.Join(solutionDir, relative_path)));
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

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // load additional project data from file
        static string DetermineRealFilename(string pa)
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
        
        foreach (var (project, projectRelativePath) in loads)
        {
            var pathToProjectFile = DetermineRealFilename(projectRelativePath);
            if (pathToProjectFile == "")
            {
                continue;
            }
            if (File.Exists(pathToProjectFile) == false)
            {
                printer.Info($"Unable to open project file: {pathToProjectFile}");
                continue;
            }
            var document = new XmlDocument();
            document.Load(pathToProjectFile);
            var root_element = document.DocumentElement;
            if (root_element == null) { printer.Error($"Failed to load {projectRelativePath}"); continue; }
            var namespace_match = Regex.Match("\\{.*\\}", root_element.Name);
            var xmlNamespace = namespace_match.Success ? namespace_match.Groups[0].Value : "";
            HashSet<string> configurations = new();
            foreach (var n in root_element.ElementsNamed("VisualStudioProject").ElementsNamed("Configurations").ElementsNamed("Configuration"))
            {
                if (n.Attributes == null) { continue; }
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
                    printer.Info($"Unknown build type in {pathToProjectFile}: {inner_text}");
                }
            }

            var configurationTypes = root_element.ElementsNamed("PropertyGroup").ElementsNamed("ConfigurationType").ToImmutableHashSet();
            foreach(var n in configurationTypes)
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
                    printer.Info($"Unknown build type in {pathToProjectFile}: {inner_text}");
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
            var projectGuid = dependency.ToLowerInvariant();
            if (projects.TryGetValue(projectGuid, out var project))
            {
                solution.AddDependency(from, project);
            }
            else
            {
                // todo(Gustav) look into theese warnings...!
                // printer.Info("Missing reference ", s)
                // pass
            }
        }

        return solution;
    }

}
