using Spectre.Console;
using Workbench.CMake;

namespace Workbench
{
    public class Solution
    {
        public List<Project> JustProjects { get; set; }
        public Dictionary<string, Project> Projects { get; private set; }

        public Solution(Dictionary<string, Project> allProjects, List<Project> projects)
        {
            JustProjects = projects;
            Projects = allProjects;
        }

        public enum ProjectType
        {
            Interface, Executable, Static, Shared
        }

        public class Project
        {
            public Project(ProjectType type, string name)
            {
                Type = type;
                Name = name;
            }

            public ProjectType Type { get; }
            public string Name { get; }
            public List<Project> Dependencies { get; set; } = new();
            public List<string> NamedDependencies { get; set; } = new();

            internal void Resolve(Dictionary<string, Project> map)
            {
                Dependencies = NamedDependencies.Where(name => map.ContainsKey(name)).Select(name => map[name]).ToList();
            }

            public override string ToString()
            {
                return $"{Name} {Type}";
            }
        }

        public static Solution Parse(IEnumerable<CMake.Trace> lines)
        {
            CmakeSolutionParser parser = new();
            foreach(var line in lines)
            {
                switch(line.Cmd.ToLower())
                {
                    case "add_executable": parser.AddExecutable(line); break;
                    case "add_library": parser.AddLibrary(line); break;
                    case "target_link_libraries": parser.LinkLibrary(line); break;
                }
            }
            return parser.CreateSolution();
        }

        private static IEnumerable<string> ChildrenNames(Project proj, bool add)
        {
            foreach(var p in proj.Dependencies)
            {
                if(add)
                {
                    yield return p.Name;
                }

                foreach(var n in ChildrenNames(p, true))
                {
                    yield return n;
                }
            }
        }
        
        public void Simplify()
        {
            /*
            given the dependencies like:
            a -> b
            b -> c
            a -> c
            simplify will remove the last dependency (a->c) to 'simplify' the graph
            */
            foreach(var project in JustProjects)
            {
                var se = ChildrenNames(project, false).ToHashSet();
                List<string> dependencies = new();
                foreach (var dependency in project.Dependencies)
                {
                    //if(has_dependency(project, dependency_name, false) == false)
                    if(se.Contains(dependency.Name) == false)
                    {
                        dependencies.Add(dependency.Name);
                    }
                }
                project.NamedDependencies = dependencies;
            }
            PostLoad();
        }

        internal Graphviz MakeGraphviz(bool reverse, bool removeEmpty, IEnumerable<string> namesToIgnore)
        {
            Graphviz gv = new();

            var ni = namesToIgnore.Select(x => x.ToLower().Trim()).ToHashSet();

            var projects = JustProjects.ToArray();

            if(removeEmpty)
            {
                var names = projects.SelectMany(p => p.Dependencies).Select(p => p.Name).ToHashSet();
                projects = projects.Where(p => p.Dependencies.Count > 0 || names.Contains(p.Name)).ToArray();
            }

            Dictionary<string, Graphviz.Node> nodes = new();
            foreach (var p in projects)
            {
                if(ni.Contains(p.Name.ToLower().Trim())) continue;
                nodes.Add(p.Name, gv.AddNode(p.Name, GetGraphvizType(p)));
            }

            foreach (var p in projects)
            {
                if (ni.Contains(p.Name.ToLower().Trim())) continue;
                foreach (var to in p.Dependencies)
                {
                    if (ni.Contains(to.Name.ToLower().Trim())) continue;
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

        internal void RemoveProjects(string[] names)
        {
            var set = names.ToHashSet();

            // remove links
            foreach(var p in this.JustProjects)
            {
                p.NamedDependencies = p.NamedDependencies.Where(n => set.Contains(n) == false).ToList();
            }

            // remove projects
            JustProjects = this.JustProjects.Where(p => set.Contains(p.Name) == false).ToList();

            // remove links
            // a name can be 2 keys (alias)
            var keysToRemove = Projects.Where(p => set.Contains(p.Value.Name)).Select(p => p.Key).ToArray();
            foreach(var key in keysToRemove)
            {
                Projects.Remove(key);
            }

            PostLoad();
        }

        private string GetGraphvizType(Project p)
        {
            switch (p.Type)
            {
                case ProjectType.Executable:
                    return "folder";
                case ProjectType.Static:
                    return "component";
                case ProjectType.Shared:
                    return "ellipse";
            }

            return "plaintext";
        }

        public void PostLoad()
        {
            foreach(var p in this.JustProjects)
            {
                p.Resolve(Projects);
            }
        }

        private bool has_dependency(Project project, string dependency_name, bool self_reference)
        {
            foreach(var current_name in project.NamedDependencies)
            {
                if(self_reference && current_name == dependency_name)
                {
                    return true;
                }
                if(Projects.ContainsKey(current_name) == false)
                {
                    return false;
                }
                if(has_dependency(Projects[current_name], dependency_name, true))
                {
                    return true;
                }
            }
            return false;
        }
    }

    internal class CmakeSolutionParser
    {
        Dictionary<string, Solution.Project> allProjects = new();
        List<Solution.Project> projects = new();

        internal void AddExecutable(Trace line)
        {
            var app = line.Args[0];
            AnsiConsole.MarkupLine($"Adding executable {app}");
            var p = new Solution.Project(Solution.ProjectType.Executable, app);
            allProjects.Add(app, p);
            projects.Add(p);
        }

        internal void AddLibrary(Trace line)
        {
            var lib = line.Args[0];
            AnsiConsole.MarkupLine($"Adding lib {lib}");
            if(allProjects.ContainsKey(lib))
            {
                // todo(Gustav): add warning
                return;
            }

            if (line.Args.Contains("ALIAS"))
            {
                var name = line.Args[2];
                allProjects.Add(lib, allProjects[name]);
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

            var p = new Solution.Project(libType, lib);
            allProjects.Add(lib, p);
            projects.Add(p);
        }

        internal void LinkLibrary(Trace line)
        {
            var targetName = line.Args[0];

            if (allProjects.ContainsKey(targetName) == false)
            {
                // todo(Gustav): add warning
                return;
            }
            var target = allProjects[targetName];
            foreach (var c in line.Args.Skip(1))
            {
                if (c == "PUBLIC" || c == "INTERFACE" || c == "PRIVATE") { continue; }
                target.NamedDependencies.Add(c);
            }
        }

        internal Solution CreateSolution()
        {
            var s = new Solution(allProjects, projects);
            s.PostLoad();
            return s;
        }
    }
}
