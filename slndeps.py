#!/usr/bin/env python3

import argparse
import os
import os.path
import xml.etree.ElementTree as ET
from enum import Enum
import re
import os
import subprocess
import typing


# ======================================================================================================================
# project
# ======================================================================================================================

class Build(Enum):
        Unknown = 1
        Application = 2
        Static = 3
        Shared = 4


class Project:
    def __init__(self, Solution: 'Solution', Name: str, Path: str, Id: str):
        self.DisplayName = ""
        self.Type = Build.Unknown
        self.deps: typing.List['Project'] = []
        self.sdeps: typing.List[str] = []

        self.Solution = Solution
        self.Name = Name
        self.Path = Path
        self.Id = Id

    @property
    def Name(self) -> str:
        return self.DisplayName.replace('-', '_').replace('.', '_')

    @Name.setter
    def Name(self, value: str):
        self.DisplayName = value

    def __str__(self):
        return self.Name

    def resolve(self, projects: typing.Dict[str, 'Project']):
        # print("Resolving ", self.Name, len(self.sdeps))
        for ss in self.sdeps:
            s = ss.lower()
            if s in projects:
                self.deps.append(projects[s])
            else:
                print("Missing reference ", s)

    def loadInformation(self):
        p = Gen(self.Path)
        if p == "":
            return
        if not os.path.isfile(p):
            print("Unable to open project file: ", p)
            return
        document = ET.parse(p)
        doc = document.getroot()
        namespace = get_namespace(doc)
        l = []
        """:type : list[string]"""
        for n in doc.findall("{0}VisualStudioProject/{0}Configurations/{0}Configuration[@ConfigurationType]".format(namespace)):
            v = n.attrib["ConfigurationType"]
            if v in l is False:
                l.append(v)
        self.Type = Build.Unknown

        if len(l) != 0:
            suggestedType = l[0]
            if suggestedType == "2":
                self.Type = Build.Shared
            elif suggestedType == "4":
                self.Type = Build.Static
            elif suggestedType == "1":
                self.Type = Build.Application
        for n in doc.findall("./{0}PropertyGroup/{0}OutputType".format(namespace)):
            inner_text = n.text.strip().lower()
            if inner_text == "winexe":
                self.Type = Build.Application
            elif inner_text == "exe":
                self.Type = Build.Application
            elif inner_text == "library":
                self.Type = Build.Shared
            else:
                print("Unknown build type in ", p, ": ", inner_text)
        for n in doc.findall("./{0}ItemGroup/{0}ProjectReference/{0}Project".format(namespace)):
            inner_text = n.text.strip()
            self.sdeps_append(inner_text)

    def sdeps_append(self, dep: str):
        self.sdeps.append(dep.lower())
        pass


def Gen(pa: str) -> str:
    p = pa
    if os.path.isfile(p):
        return p
    p = pa + ".vcxproj"
    if os.path.isfile(p):
        return p
    p = pa + ".csproj"
    if os.path.isfile(p):
        return p
    return ""


def get_namespace(element):
    m = re.match('\{.*\}', element.tag)
    return m.group(0) if m else ''


# ======================================================================================================================
# solution
# ======================================================================================================================

class Solution:
    projects = {}
    Name = ""
    includes = []
    reverseArrows = False
    """ @type projects: dict[string, Project] """
    """ @type includes: list[string] """

    def __init__(self, slnpath: str, exclude: typing.List[str], simplify: bool, reverseArrows: bool):
        self.reverseArrows = reverseArrows
        self.setup(slnpath, exclude, simplify)

    def setup(self, slnpath: str, exclude: typing.List[str], dosimplify: bool):
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
                currentProject = Project(Solution=self, Name=name, Path=os.path.join(os.path.dirname(slnpath), relativepath), Id=id)
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
    def Graphviz(self) -> typing.Iterable[str]:
        yield "digraph " + self.Name.replace("-", "_") + " {"
        yield "/* projects */"
        for _, pro in self.projects.items():
            if self.Exclude(pro.DisplayName):
                continue
            decoration = "label=\"" + pro.DisplayName + "\""
            shape = "plaintext"
            if pro.Type == Build.Application:
                shape = "folder"
            elif pro.Type == Build.Shared:
                shape = "ellipse"
            elif pro.Type == Build.Static:
                shape = "component"
            decoration += ", shape=" + shape
            yield " " + pro.Name + " [" + decoration + "]" + ";"
        yield ""
        yield "/* dependencies */"
        addspace = False
        for _, p in self.projects.items():
            if len(p.deps) == 0:
                # print("not enough ", p.Name)
                continue
            if self.Exclude(p.Name):
                continue
            if addspace:
                yield ""
            else:
                addspace = True
            for s in p.deps:
                if self.Exclude(s.DisplayName):
                    continue
                if self.reverseArrows:
                    yield " " + s.Name + " -> " + p.Name + ";"
                else:
                    yield " " + p.Name + " -> " + s.Name + ";"
        yield "}"

    def Exclude(self, p: str) -> bool:
        if self.includes is not None:
            for s in self.includes:
                if s.lower().strip() == p.lower().strip():
                    return True
        return False

    def writeGraphviz(self, targetFile: str):
        with open(targetFile, 'w') as f:
            for line in self.Graphviz:
                f.write(line + "\n")

    def simplify(self):
        for _, pe in self.projects.items():
            self.ssimplify(pe)

    def ssimplify(self, p: Project):
        deps = []
        for d in p.sdeps:
            if not self.hasDependency(p, d, False):
                deps.append(d)
        p.sdeps = deps

    def hasDependency(self, p: Project, sd: str, self_reference: bool) -> bool:
        for d in p.sdeps:
            if self_reference and d == sd:
                return True
            if self.hasDependency(self.projects[d], sd, True):
                return True
        return False


def IsValidFile(lines: typing.List[str]) -> bool:
    for line in lines:
        if line == "Microsoft Visual Studio Solution File, Format Version 10.00":
            return True
    return False


# ======================================================================================================================
# logic
# ======================================================================================================================


def getFile(source: str, target: str, format: str) -> str:
    my_target = target
    if my_target.strip() == "" or my_target.strip() == "?":
        my_target = os.path.splitext(source)[0]
    if os.path.isdir(my_target):
        my_target = os.path.join(my_target, os.path.splitext(os.path.basename(source))[0])
    my_format = format
    if my_format.strip() == "" or my_format.strip() == "?":
        my_format = "svg"
    f = my_target + "." + my_format
    return f


def GetProjects(source: str) -> typing.Iterable[str]:
    s = Solution(source, [], False, False)
    for p in s.projects:
        yield p.Value.Name


def graphviz(targetFile: str, format: str, style: str):
    cmdline = ["dot", targetFile+".graphviz", "-T" + format, "-K" + style, "-O" + targetFile + "." + format]
    print("Running graphviz ", cmdline)
    s = subprocess.call(cmdline)


def logic(solutionFilePath: str, exlude: typing.List[str], targetFile: str, format: str, simplify: bool, style: str, reverseArrows: bool):
    s = Solution(solutionFilePath, exlude, simplify, reverseArrows)
    s.writeGraphviz(targetFile + ".graphviz")
    graphviz(targetFile, format, style)


def toGraphviz(source: str, target: str, format: str, exclude: typing.List[str], simplify: bool, style: bool, reverseArrows: bool):
    my_target = target or ""
    if my_target.strip() == "" or my_target.strip() == "?":
        my_target = os.path.splitext(source)[0]
    my_format = format or ""
    if my_format.strip() == "" or my_format.strip() == "?":
        my_format = "svg"
    logic(source, exclude or [], my_target, my_format, simplify, style or "dot", reverseArrows)


# ======================================================================================================================
# main
# ======================================================================================================================

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description='Generate a solution dependency file')
    # parser.add_argument('cmd', help='the command to execute')
    parser.add_argument('solution', help='the solution')

    parser.add_argument('--target', help='the target')
    parser.add_argument('--format', help='the format')
    parser.add_argument('--exclude', help='projects to exclude', nargs='*')
    parser.add_argument('--simplify', dest='simplify', action='store_const', const=True, default=False, help='simplify output')
    parser.add_argument('--style', help='the style')
    parser.add_argument('--reverse', dest='reverse', action='store_const', const=True, default=False, help='reverse arrows')

    args = parser.parse_args()
    toGraphviz(args.solution, args.target, args.format, args.exclude, args.simplify, args.style, args.reverse)
