using Spectre.Console;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Workbench.CMake;

namespace Workbench;

public class Solution
{
    private readonly Dictionary<Guid, Project> projects = new();
    private readonly Dictionary<Guid, List<Guid>> uses = new();
    private readonly Dictionary<Guid, List<Guid>> isUsedBy = new();

    private List<Guid> Uses(Project p) => this.uses[p.Guid]; // app uses lib
    private List<Guid> IsUsedBy(Project p) => this.isUsedBy[p.Guid]; // lib is used by app

    public enum ProjectType
    {
        Interface, Executable, Static, Shared
    }

    public class Project
    {
        public Solution Solution { get; }
        public ProjectType Type { get; }
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
            return $"{Name} {Type}";
        }

        public IEnumerable<Project> Uses => Solution.Uses(this).Select(dep => Solution.projects[dep]);
        public IEnumerable<Project> IsUsedBy => Solution.IsUsedBy(this).Select(dep => Solution.projects[dep]);
    }

    private static IEnumerable<string> ChildrenNames(Project proj, bool add)
    {
        foreach (var p in proj.Uses)
        {
            if (add)
            {
                yield return p.Name;
            }

            foreach (var n in ChildrenNames(p, true))
            {
                yield return n;
            }
        }
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
        var guids = this.projects.Values.Where(predicate).Select(p => p.Guid).ToImmutableArray();
        
        foreach(var g in guids)
        {
            this.projects.Remove(g);
            this.uses.Remove(g);
            this.isUsedBy.Remove(g);
        }

        return guids.Length;
    }

    private void PostLoad()
    {
#if false
        foreach (var p in Projects)
        {
            p.Resolve(AliasToProject);
        }
#endif
    }

    private bool has_dependency(Project project, string dependency_name, bool self_reference)
    {
#if false
        foreach (var current_name in project.NamedDependencies)
        {
            if (self_reference && current_name == dependency_name)
            {
                return true;
            }
            if (AliasToProject.ContainsKey(current_name) == false)
            {
                return false;
            }
            if (has_dependency(AliasToProject[current_name], dependency_name, true))
            {
                return true;
            }
        }
        return false;
    }
#else
        return false;
#endif
    }

    public Project AddProject(ProjectType type, string name)
    {
        var project = new Project(this, type, name, new Guid());
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
}
