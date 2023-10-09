using Spectre.Console;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using System.Xml;
using Workbench.CMake;
using Workbench.Utils;
using static Workbench.Solution;

namespace Workbench;

public class Solution
{
    private readonly Dictionary<Guid, Project> projects = new();

    // list always contains at least one item (unless it has been removed), missing entry means 0 items
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
                ProjectType.Executable => Shape.Folder,
                ProjectType.Static => Shape.Component,
                ProjectType.Shared => Shape.Ellipse,
                _ => Shape.PlainText,
            };
            nodes.Add(p.Name, gv.AddNode(p.Name, nodeShape));
        }

        foreach (var p in this.projects.Values)
        {
            foreach (var to in p.Uses)
            {
                var nodeFrom = nodes[p.Name];
                var nodeTo = nodes[to.Name];
                if (reverse == false)
                {
                    gv.AddEdge(nodeFrom, nodeTo);
                }
                else
                {
                    gv.AddEdge(nodeTo, nodeFrom);
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
            Project? currentProject = null;
            var projectLine = string.Empty;
            var solutionDir = new FileInfo(solution_path).Directory?.FullName!;
            foreach (var line in lines)
            {
                if (line.StartsWith("Project"))
                {
                    // this might be a "folder" or a actual project
                    projectLine = line;
                }
                else if (line.Trim() == "ProjectSection(ProjectDependencies) = postProject")
                {
                    // it is a project
                    var equalIndex = projectLine.IndexOf('=');
                    var data = projectLine.Substring(equalIndex + 1).Split(',', StringSplitOptions.RemoveEmptyEntries);
                    var name = data[0].Trim().Trim('\"').Trim();
                    var relativePath = data[1].Trim().Trim('\"').Trim();
                    var projectGuid = data[2].Trim().Trim('\"').Trim();
                    // todo(Gustav): reuse guid?
                    currentProject = solution.AddProject(Solution.ProjectType.Unknown, name);
                    loads.Add(new(currentProject, Path.Join(solutionDir, relativePath)));
                    projects[projectGuid.ToLowerInvariant()] = currentProject;
                }
                else if (currentProject != null && line.Trim().StartsWith("{"))
                {
                    var projectGuid = line.Split('=')[0].Trim();
                    dependencies.Add(new(currentProject, projectGuid));
                }
                else if (line == "EndProject")
                {
                    currentProject = null;
                    projectLine = string.Empty;
                }
            }
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
            var rootElement = document.DocumentElement;
            if (rootElement == null) { printer.Error($"Failed to load {projectRelativePath}"); continue; }
            var namespaceMatch = Regex.Match("\\{.*\\}", rootElement.Name);
            var xmlNamespace = namespaceMatch.Success ? namespaceMatch.Groups[0].Value : "";
            HashSet<string> configurations = new();
            foreach (var n in rootElement.ElementsNamed("VisualStudioProject").ElementsNamed("Configurations").ElementsNamed("Configuration"))
            {
                var configurationType = n.Attributes["ConfigurationType"];
                if (configurationType != null)
                {
                    configurations.Add(configurationType.Value);
                }
            }

            if (configurations.Count != 0)
            {
                var suggestedType = configurations.FirstOrDefault() ?? "";
                if (suggestedType == "2")
                {
                    project.Type = ProjectType.Shared;
                }
                else if (suggestedType == "4")
                {
                    project.Type = ProjectType.Static;
                }
                else if (suggestedType == "1")
                {
                    project.Type = ProjectType.Executable;
                }
            }

            foreach (var n in rootElement.ElementsNamed("PropertyGroup").ElementsNamed("OutputType"))
            {
                var innerText = n.InnerText.Trim().ToLowerInvariant();
                if (innerText == "winexe")
                {
                    project.Type = ProjectType.Executable;
                }
                else if (innerText == "exe")
                {
                    project.Type = ProjectType.Executable;
                }
                else if (innerText == "library")
                {
                    project.Type = ProjectType.Shared;
                }
                else
                {
                    printer.Info($"Unknown build type in {pathToProjectFile}: {innerText}");
                }
            }

            var configurationTypes = rootElement.ElementsNamed("PropertyGroup").ElementsNamed("ConfigurationType").ToImmutableHashSet();
            foreach(var n in configurationTypes)
            {
                var innerText = n.InnerText.Trim().ToLowerInvariant();
                if (innerText == "utility")
                {
                }
                else if (innerText == "staticlibrary")
                {
                    project.Type = ProjectType.Static;
                }
                else if (innerText == "application")
                {
                    project.Type = ProjectType.Executable;
                }
                else
                {
                    printer.Info($"Unknown build type in {pathToProjectFile}: {innerText}");
                }
            }

            foreach (var n in rootElement.ElementsNamed("ItemGroup").ElementsNamed("ProjectReference").ElementsNamed("Project"))
            {
                var innerText = n.InnerText.Trim();
                dependencies.Add(new(project, innerText));
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
                // todo(Gustav) look into these warnings...!
                // printer.Info("Missing reference ", s)
                // pass
            }
        }

        return solution;

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
    }

}
