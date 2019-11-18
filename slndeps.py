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
        self.Type = Build.Unknown
        self.dependencies: typing.List['Project'] = []
        self.named_dependencies: typing.List[str] = []

        self.solution = solution
        self.display_name = name
        self.path = path
        self.guid = guid

    def get_safe_name(self) -> str:
        return self.display_name.replace(' ', '_').replace('-', '_').replace('.', '_')

    def __str__(self):
        return self.get_safe_name()

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
        p = determine_real_filename(self.path)
        if p == "":
            return
        if not os.path.isfile(p):
            print("Unable to open project file: ", p)
            return
        document = ET.parse(p)
        root_element = document.getroot()
        namespace_match = re.match('\{.*\}', root_element.tag)
        namespace = namespace_match.group(0) if namespace_match else ''
        configurations: typing.List[str] = []
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


# ======================================================================================================================
# solution
# ======================================================================================================================

class Solution:
    projects: typing.Dict[str, Project] = {}
    Name = ""

    def __init__(self, solution_path: str):
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
                self.projects[current_project.guid.lower()] = current_project
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
        self.post_load()

    def post_load(self):
        for project_id, p in self.projects.items():
            p.resolve(self.projects)

    def generate_graphviz_source_lines(self, exclude: typing.List[str], reverse_arrows: bool) -> typing.Iterable[str]:
        project_names_to_exclude = [name.lower().strip() for name in exclude]
        yield "digraph " + self.Name.replace("-", "_") + " {"
        yield "/* projects */"
        for _, project in self.projects.items():
            if project.display_name.lower().strip() in project_names_to_exclude:
                continue
            decoration = "label=\"" + project.display_name + "\""
            shape = "plaintext"
            if project.Type == Build.Application:
                shape = "folder"
            elif project.Type == Build.Shared:
                shape = "ellipse"
            elif project.Type == Build.Static:
                shape = "component"
            decoration += ", shape=" + shape
            yield " " + project.get_safe_name() + " [" + decoration + "]" + ";"
        yield ""
        yield "/* dependencies */"
        first_project = True
        for _, project in self.projects.items():
            if len(project.dependencies) == 0:
                # print("not enough ", project.Name)
                continue
            if project.display_name.lower().strip() in project_names_to_exclude:
                continue
            if not first_project:
                yield ""
            else:
                first_project = False
            for dependency in project.dependencies:
                if dependency.display_name.lower().strip() in project_names_to_exclude:
                    continue
                if reverse_arrows:
                    yield " " + dependency.get_safe_name() + " -> " + project.get_safe_name() + ";"
                else:
                    yield " " + project.get_safe_name() + " -> " + dependency.get_safe_name() + ";"
        yield "}"

    def write_graphviz(self, target_file: str, exclude: typing.List[str], reverse_arrows: bool):
        lines = list(self.generate_graphviz_source_lines(exclude, reverse_arrows))
        with open(target_file, 'w') as f:
            for line in lines:
                f.write(line + "\n")

    def simplify(self):
        """
        given the dependencies like:
        a -> b
        b -> c
        a -> c
        simplify will remove the last dependency (a->c) to 'simplify' the graph
        """
        for _, project in self.projects.items():
            dependencies = []
            for dependency_name in project.named_dependencies:
                if not self.has_dependency(project, dependency_name, False):
                    dependencies.append(dependency_name)
            project.named_dependencies = dependencies
        self.post_load()

    def has_dependency(self, project: Project, dependency_name: str, self_reference: bool) -> bool:
        for current_name in project.named_dependencies:
            if self_reference and current_name == dependency_name:
                return True
            if self.has_dependency(self.projects[current_name], dependency_name, True):
                return True
        return False


# ======================================================================================================================
# logic
# ======================================================================================================================

def run_graphviz(target_file: str, image_format: str, graphviz_layout: str):
    cmdline = [
        "dot",
        target_file + ".graphviz", "-T" + image_format,
        "-K" + graphviz_layout,
        "-O" + target_file + "." + image_format
    ]
    print("Running graphviz ", cmdline)
    s = subprocess.call(cmdline)


def value_or_default(value: str, default: str):
    return default if value.strip() == "" or value.strip() == "?" else value


# ======================================================================================================================
# Handlers
# ======================================================================================================================

def handle_generate(args):
    path_to_solution_file = args.solution
    exclude = args.exclude or []
    simplify = args.simplify
    graphviz_layout = args.style or "dot"
    reverse_arrows = args.reverse

    solution = Solution(path_to_solution_file)
    if simplify:
        solution.simplify()

    target_file = value_or_default(args.target or "", os.path.splitext(path_to_solution_file)[0])
    image_format = value_or_default(args.format or "", "svg")
    solution.write_graphviz(target_file + ".graphviz", exclude, reverse_arrows)
    run_graphviz(target_file, image_format, graphviz_layout)


def handle_source(args):
    path_to_solution_file = args.solution
    exclude = args.exclude or []
    simplify = args.simplify
    reverse_arrows = args.reverse
    solution = Solution(path_to_solution_file)
    if simplify:
        solution.simplify()

    lines = solution.generate_graphviz_source_lines(exclude, reverse_arrows)
    for line in lines:
        print(line)


def handle_list(args):
    solution = Solution(args.solution)
    for project_guid, project in solution.projects.items():
        print(project.display_name)

    print('')


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

    sub = sub_parsers.add_parser('source', help='Display graphviz dependency file')
    sub.add_argument('solution', help='the solution')
    sub.add_argument('--target', help='the target')
    sub.add_argument('--format', help='the format')
    sub.add_argument('--exclude', help='projects to exclude', nargs='*')
    sub.add_argument('--simplify', dest='simplify', action='store_const', const=True, default=False,
                     help='simplify output')
    sub.add_argument('--style', help='the style')
    sub.add_argument('--reverse', dest='reverse', action='store_const', const=True, default=False,
                     help='reverse arrows')
    sub.set_defaults(func=handle_source)

    sub = sub_parsers.add_parser('list', help='List projects')
    sub.add_argument('solution', help='the solution')
    sub.set_defaults(func=handle_list)

    args = parser.parse_args()
    if args.command_name is not None:
        args.func(args)
    else:
        parser.print_help()

