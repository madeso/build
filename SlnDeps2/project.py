from enum import Enum
import os.path
import xml.etree.ElementTree as ET
import re


class Build(Enum):
        Unknown = 1
        Application = 2
        Static = 3
        Shared = 4


class Project:
    """
    :type Solution: Solution
    :type DisplayName: string
    :type Path: string
    :type Id: string
    :type Type: Build
    :type deps: list[Project]
    :type sdeps: list[string]
    """

    def __init__(self, Solution, Name, Path, Id):
        """
        :type Solution: Solution
        :type Name: string
        :type Path: string
        :type Id: string
        """

        self.DisplayName = ""
        self.Type = Build.Unknown
        self.deps = []
        self.sdeps = []

        self.Solution = Solution
        self.Name = Name
        self.Path = Path
        self.Id = Id

    @property
    def Name(self):
        """
        @retype: string
        """
        return self.DisplayName.replace('-', '_').replace('.', '_')

    @Name.setter
    def Name(self, value):
        """
        :param value: string
        """
        self.DisplayName = value

    def __str__(self):
        return self.Name

    def resolve(self, projects):
        """
        :param projects: dict[string, Project]
        """
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

    def sdeps_append(self, dep):
        """
        :type dep: string
        """
        self.sdeps.append(dep.lower())
        pass

def Gen(pa):
    """
    :param pa: string
    :return: string
    """
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