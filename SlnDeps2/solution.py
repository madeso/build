import os
import project


class Solution:
    projects = {}
    Name = ""
    includes = []
    reverseArrows = False
    """ @type projects: dict[string, Project] """
    """ @type includes: list[string] """

    def __init__(self, slnpath, exclude, simplify, reverseArrows):
        """
        @type slnpath: string
        @type exclude:  list[string]
        @type simplify: bool
        @type reverseArrows: bool
        """
        self.reverseArrows = reverseArrows
        self.setup(slnpath, exclude, simplify)

    def setup(self, slnpath, exclude, dosimplify):
        """
        @type slnpath: string
        @type exclude:  list[string]
        @type dosimplify: bool
        """
        self.includes = exclude
        self.Name = os.path.basename(os.path.splitext(slnpath)[0])
        with open(slnpath) as f:
            lines = f.readlines()
        currentProject = None
        depProject = None
        for line in lines:
            if line.startswith("Project"):
                eq = line.find('=')
                data = line[eq + 1:].split(",")  # , StringSplitOptions.RemoveEmptyEntries)
                name = data[0].strip().strip('"').strip()
                relativepath = data[1].strip().strip('"').strip()
                id = data[2].strip().strip('"').strip()
                currentProject = project.Project(Solution=self, Name=name, Path=os.path.join(os.path.dirname(slnpath), relativepath), Id=id)
                self.projects[currentProject.Id.lower()] = currentProject
            elif line == "EndProject":
                currentProject = None
                depProject = None
            elif line.strip() == "ProjectSection(ProjectDependencies) = postProject":
                depProject = currentProject
            elif depProject != None and line.strip().startswith("{"):
                id = line.split("=")[0].strip()
                depProject.sdeps_append(id)
        for project_id, p in self.projects.items():
            p.loadInformation()
        if dosimplify:
            self.simplify()
        for project_id, p in self.projects.items():
            p.resolve(self.projects)

    @property
    def Graphviz(self):
        """
        @retype: list[string]
        """
        lines = []
        """:type : list[string]"""
        lines.append("digraph " + self.Name.replace("-", "_") + " {")
        lines.append("/* projects */")
        for _, pro in self.projects.items():
            if self.Exclude(pro.DisplayName):
                continue
            decoration = "label=\"" + pro.DisplayName + "\""
            shape = "plaintext"
            if pro.Type == project.Build.Application:
                shape = "folder"
            elif pro.Type == project.Build.Shared:
                shape = "ellipse"
            elif pro.Type == project.Build.Static:
                shape = "component"
            decoration += ", shape=" + shape
            lines.append(" " + pro.Name + " [" + decoration + "]" + ";")
        lines.append("")
        lines.append("/* dependencies */")
        addspace = False
        for _, p in self.projects.items():
            if len(p.deps) == 0:
                # print("not enough ", p.Name)
                continue
            if self.Exclude(p.Name):
                continue
            if addspace:
                lines.append("")
            else:
                addspace = True
            for s in p.deps:
                if self.Exclude(s.DisplayName):
                    continue
                if self.reverseArrows:
                    lines.append(" " + s.Name + " -> " + p.Name + ";")
                else:
                    lines.append(" " + p.Name + " -> " + s.Name + ";")
        lines.append("}")
        return lines

    def Exclude(self, p):
        """
        @type p: string
        @retype: bool
        """
        if self.includes is not None:
            for s in self.includes:
                if s.lower().strip() == p.lower().strip():
                    return True
        return False

    def writeGraphviz(self, targetFile):
        """
        @type targetFile: string
        """
        with open(targetFile, 'w') as f:
            for line in self.Graphviz:
                f.write(line + "\n")

    def simplify(self):
        """
        """
        for _, pe in self.projects.items():
            self.ssimplify(pe)

    def ssimplify(self, p):
        """
        @type p: Project
        """
        deps = []
        for d in p.sdeps:
            if not self.hasDependency(p, d, False):
                deps.append(d)
        p.sdeps = deps

    def hasDependency(self, p, sd, self_reference):
        """
        @type p: Project
        @type sd: string
        @type self_reference: bool
        @retype: bool
        """
        for d in p.sdeps:
            if self_reference and d == sd:
                return True
            if self.hasDependency(self.projects[d], sd, True):
                return True
        return False


def IsValidFile(lines):
    """
    static
    @type lines: list[string]
    @retype bool
    """
    for line in lines:
        if line == "Microsoft Visual Studio Solution File, Format Version 10.00":
            return True
    return False