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


def determine_real_filename(pa: str) -> str:
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


class Project:
    def __init__(self, solution: 'Solution', name: str, path: str, guid: str):
        self.DisplayName = ""
        self.Type = Build.Unknown
        self.dependencies: typing.List['Project'] = []
        self.named_dependencies: typing.List[str] = []

        self.Solution = solution
        self.Name = name
        self.Path = path
        self.Id = guid

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
        for dependency in self.named_dependencies:
            s = dependency.lower()
            if s in projects:
                self.dependencies.append(projects[s])
            else:
                # todo(Gustav): look into theese warnings...!
                # print("Missing reference ", s)
                pass

    def load_information(self):
        p = determine_real_filename(self.Path)
        if p == "":
            return
        if not os.path.isfile(p):
            print("Unable to open project file: ", p)
            return
        document = ET.parse(p)
        root_element = document.getroot()
        namespace = get_namespace(root_element)
        configurations:typing.List[str] = []
        for n in root_element.findall("{0}VisualStudioProject/{0}Configurations/{0}Configuration[@ConfigurationType]".format(namespace)):
            configuration_type = n.attrib["ConfigurationType"]
            if configuration_type in configurations is False:
                configurations.append(configuration_type)
        self.Type = Build.Unknown

        if len(configurations) != 0:
            suggested_type = configurations[0]
            if suggested_type == "2":
                self.Type = Build.Shared
            elif suggested_type == "4":
                self.Type = Build.Static
            elif suggested_type == "1":
                self.Type = Build.Application
        for n in root_element.findall("./{0}PropertyGroup/{0}OutputType".format(namespace)):
            inner_text = n.text.strip().lower()
            if inner_text == "winexe":
                self.Type = Build.Application
            elif inner_text == "exe":
                self.Type = Build.Application
            elif inner_text == "library":
                self.Type = Build.Shared
            else:
                print("Unknown build type in ", p, ": ", inner_text)
        for n in root_element.findall("./{0}ItemGroup/{0}ProjectReference/{0}Project".format(namespace)):
            inner_text = n.text.strip()
            self.add_named_dependency(inner_text)

    def add_named_dependency(self, dep: str):
        self.named_dependencies.append(dep.lower())
        pass


def get_namespace(element):
    m = re.match('\{.*\}', element.tag)
    return m.group(0) if m else ''


# ======================================================================================================================
# solution
# ======================================================================================================================

class Solution:
    projects: typing.Dict[str, Project] = {}
    Name = ""
    includes: typing.List[str] = []
    reverseArrows = False

    def __init__(self, solution_path: str, exclude: typing.List[str], simplify: bool, reverse_arrows: bool):
        self.reverseArrows = reverse_arrows
        self.setup(solution_path, exclude, simplify)

    def setup(self, solution_path: str, exclude: typing.List[str], do_simplify: bool):
        self.includes = exclude
        self.Name = os.path.basename(os.path.splitext(solution_path)[0])
        with open(solution_path) as f:
            lines = f.readlines()
        current_project = None
        dependency_project = None
        for line in lines:
            if line.startswith("Project"):
                equal_index = line.find('=')
                data = line[equal_index + 1:].split(",")  # , StringSplitOptions.RemoveEmptyEntries)
                name = data[0].strip().strip('"').strip()
                relative_path = data[1].strip().strip('"').strip()
                project_guid = data[2].strip().strip('"').strip()
                current_project = Project(
                    solution=self,
                    name=name,
                    path=os.path.join(os.path.dirname(solution_path), relative_path),
                    guid=project_guid
                )
                self.projects[current_project.Id.lower()] = current_project
            elif line == "EndProject":
                current_project = None
                dependency_project = None
            elif line.strip() == "ProjectSection(ProjectDependencies) = postProject":
                dependency_project = current_project
            elif dependency_project is not None and line.strip().startswith("{"):
                project_guid = line.split("=")[0].strip()
                dependency_project.add_named_dependency(project_guid)
        for project_id, p in self.projects.items():
            p.load_information()
        if do_simplify:
            self.simplify()
        for project_id, p in self.projects.items():
            p.resolve(self.projects)

    def generate_graphviz_source_lines(self) -> typing.Iterable[str]:
        yield "digraph " + self.Name.replace("-", "_") + " {"
        yield "/* projects */"
        for _, pro in self.projects.items():
            if self.should_exclude_project(pro.DisplayName):
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
        first_project = True
        for _, project in self.projects.items():
            if len(project.dependencies) == 0:
                # print("not enough ", project.Name)
                continue
            if self.should_exclude_project(project.Name):
                continue
            if not first_project:
                yield ""
            else:
                first_project = False
            for dependency in project.dependencies:
                if self.should_exclude_project(dependency.DisplayName):
                    continue
                if self.reverseArrows:
                    yield " " + dependency.Name + " -> " + project.Name + ";"
                else:
                    yield " " + project.Name + " -> " + dependency.Name + ";"
        yield "}"

    def should_exclude_project(self, project_name: str) -> bool:
        if self.includes is not None:
            for s in self.includes:
                if s.lower().strip() == project_name.lower().strip():
                    return True
        return False

    def write_graphviz(self, target_file: str):
        lines = list(self.generate_graphviz_source_lines())
        with open(target_file, 'w') as f:
            for line in lines:
                f.write(line + "\n")

    def simplify(self):
        for _, project in self.projects.items():
            dependencies = []
            for dependency_name in project.named_dependencies:
                if not self.has_dependency(project, dependency_name, False):
                    dependencies.append(dependency_name)
            project.named_dependencies = dependencies

    def has_dependency(self, project: Project, dependency_name: str, self_reference: bool) -> bool:
        for current_name in project.named_dependencies:
            if self_reference and current_name == dependency_name:
                return True
            if self.has_dependency(self.projects[current_name], dependency_name, True):
                return True
        return False


def is_valid_file(lines: typing.List[str]) -> bool:
    for line in lines:
        if line == "Microsoft Visual Studio Solution File, Format Version 10.00":
            return True
    return False


# ======================================================================================================================
# logic
# ======================================================================================================================


def getFile(source: str, target: str, image_format: str) -> str:
    my_target = target
    if my_target.strip() == "" or my_target.strip() == "?":
        my_target = os.path.splitext(source)[0]
    if os.path.isdir(my_target):
        my_target = os.path.join(my_target, os.path.splitext(os.path.basename(source))[0])
    my_format = image_format
    if my_format.strip() == "" or my_format.strip() == "?":
        my_format = "svg"
    f = my_target + "." + my_format
    return f


def GetProjects(source: str) -> typing.Iterable[str]:
    s = Solution(source, [], False, False)
    for p in s.projects:
        yield p.Value.Name


def run_graphviz(target_file: str, image_format: str, graphviz_layout: str):
    cmdline = [
        "dot",
        target_file + ".graphviz", "-T" + image_format,
        "-K" + graphviz_layout,
        "-O" + target_file + "." + image_format
    ]
    print("Running graphviz ", cmdline)
    s = subprocess.call(cmdline)


def logic(path_to_solution_file: str, exclude: typing.List[str], target_file: str, image_format: str, simplify: bool, graphviz_layout: str, reverse_arrows: bool):
    s = Solution(path_to_solution_file, exclude, simplify, reverse_arrows)
    s.write_graphviz(target_file + ".graphviz")
    run_graphviz(target_file, image_format, graphviz_layout)


def to_graphviz(path_to_solution_file: str, target: str, image_format: str, exclude: typing.List[str], simplify: bool, graphviz_layout: bool, reverse_arrows: bool):
    my_target = target or ""
    if my_target.strip() == "" or my_target.strip() == "?":
        my_target = os.path.splitext(path_to_solution_file)[0]
    my_format = image_format or ""
    if my_format.strip() == "" or my_format.strip() == "?":
        my_format = "svg"
    logic(path_to_solution_file, exclude or [], my_target, my_format, simplify, graphviz_layout or "dot", reverse_arrows)


def handle_generate(args):
    to_graphviz(args.solution, args.target, args.format, args.exclude, args.simplify, args.style, args.reverse)


# ======================================================================================================================
# main
# ======================================================================================================================

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description='Visual Studio solution dependency tool')
    sub_parsers = parser.add_subparsers(dest='command_name', title='Commands', help='', metavar='<command>')

    sub = sub_parsers.add_parser('generate', help='Generate a solution dependency file')

    sub.add_argument('solution', help='the solution')

    sub.add_argument('--target', help='the target')
    sub.add_argument('--format', help='the format')
    sub.add_argument('--exclude', help='projects to exclude', nargs='*')
    sub.add_argument('--simplify', dest='simplify', action='store_const', const=True, default=False, help='simplify output')
    sub.add_argument('--style', help='the style')
    sub.add_argument('--reverse', dest='reverse', action='store_const', const=True, default=False, help='reverse arrows')
    sub.set_defaults(func=handle_generate)

    args = parser.parse_args()
    if args.command_name is not None:
        args.func(args)
    else:
        parser.print_help()

