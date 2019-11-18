import argparse
import os
import os.path
import xml.etree.ElementTree as ET
import re
import os
import subprocess


# ======================================================================================================================
# project
# ======================================================================================================================

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


# ======================================================================================================================
# logic
# ======================================================================================================================


def toGraphviz(source, target, format, exclude, simplify, style, reverseArrows):
    """
    public
    :type source: string
    :type target: string
    :type format: string
    :type exclude: list[string]
    :type: simplify: bool
    :type style: string
    :type reverseArrows: bool
    """
    my_target = target or ""
    if my_target.strip() == "" or my_target.strip() == "?":
        my_target = os.path.splitext(source)[0]
    my_format = format or ""
    if my_format.strip() == "" or my_format.strip() == "?":
        my_format = "svg"
    logic(source, exclude or [], my_target, my_format, simplify, style or "dot", reverseArrows)

def getFile(source, target, format):
    """
    internal
    :rtype: string
    :type source: string
    :type target: string
    :type format: string
    """
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

def GetProjects(source):
    """
    public
    :rtype: IEnumerable<string>
    :type source: string
    """
    s = solution.Solution(source, [], False, False)
    for p in s.projects:
        yield p.Value.Name

def logic(solutionFilePath, exlude, targetFile, format, simplify, style, reverseArrows):
    """
    private
    :type solutionFilePath: string
    :type exlude: List<string>
    :type targetFile: string
    :type format: string
    :type simplify: bool
    :type style: string
    :type reverseArrows: bool
    """
    s = solution.Solution(solutionFilePath, exlude, simplify, reverseArrows)
    s.writeGraphviz(targetFile + ".graphviz")
    graphviz(targetFile, format, style)

def graphviz(targetFile, format, style):
    """
    private
    :type targetFile: string
    :type format: string
    :type style: string
    """
    cmdline = ["dot", targetFile+".graphviz", "-T" + format, "-K" + style, "-O" + targetFile + "." + format]
    print("Running graphviz ", cmdline)
    s = subprocess.call(cmdline)


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
    logic.toGraphviz(args.solution, args.target, args.format, args.exclude, args.simplify, args.style, args.reverse)
# using System;
# using System.Collections.Generic;
# using System.ComponentModel;
# using System.Data;
# using System.Drawing;
# using System.Linq;
# using System.Text;
# using System.Windows.Forms;
# using System.IO;
# using System.Xml;
#
# namespace SlnDeps
# {
# 	public partial class Main : Form
# 	{
# 		public Main()
# 		{
# 			InitializeComponent();
# 		}
#
# 		private void dBrowseSource_Click(object sender, EventArgs e)
# 		{
# 			if (ofd.ShowDialog() != DialogResult.OK) return;
# 			dSource.Text = ofd.FileName;
# 		}
#
# 		private void dBrowseTarget_Click(object sender, EventArgs e)
# 		{
# 			if (sfd.ShowDialog() != DialogResult.OK) return;
# 			dTarget.Text = sfd.FileName;
# 		}
#
# 		private void dGo_Click(object sender, EventArgs e)
# 		{
# 			saveSettings();
# 			List<string> items = new List<string>();
# 			foreach (var c in dExcludes.CheckedItems)
# 			{
# 				items.Add(c.ToString());
# 			}
# 			if (items.Count == 0) items = null;
# 			Logic.toGraphviz(dSource.Text, dTarget.Text, dFormat.Text, items, dSimplifyLinks.Checked, dStyle.Text, dReverseArrows.Checked);
# 		}
#
# 		private void dFillExcludes_Click(object sender, EventArgs e)
# 		{
# 			if (dExcludes.Items.Count != 0)
# 			{
# 				if (MessageBox.Show("Items is not empty, continue?", "Continue?", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
# 			}
#
# 			dExcludes.Items.Clear();
# 			Dictionary<string, string> settings = LoadSettings(ConfigurationFile);
#
# 			foreach (var s in Logic.GetProjects(dSource.Text))
# 			{
# 				dExcludes.Items.Add(s, GetBool(settings, s, true));
# 			}
# 		}
#
# 		void saveSettings()
# 		{
# 			Dictionary<string, string> settings = new Dictionary<string, string>();
# 			for (int i = 0; i < dExcludes.Items.Count; ++i )
# 			{
# 				bool ch = dExcludes.CheckedIndices.Contains(i);
# 				string name = dExcludes.Items[i].ToString();
# 				settings[name] = ch.ToString();
# 			}
#
# 			SaveSettings(ConfigurationFile, settings);
# 		}
#
# 		private string ConfigurationFile
# 		{
# 			get
# 			{
# 				return Path.ChangeExtension(dSource.Text, "slndeps");
# 			}
# 		}
#
# 		bool GetBool(Dictionary<string, string> dict, string name, bool def)
# 		{
# 			if (dict.ContainsKey(name))
# 			{
# 				return bool.Parse(dict[name]);
# 			}
# 			else return def;
# 		}
#
# 		private Dictionary<string, string> LoadSettings(string file)
# 		{
# 			Dictionary<string, string> dict = new Dictionary<string,string>();
# 			if (false == File.Exists(file)) return dict;
# 			var lines = File.ReadAllLines(file);
# 			foreach (var ol in lines)
# 			{
# 				var l = ol.Trim();
# 				if (l.StartsWith("#")) continue;
# 				var sp = l.Split("=".ToCharArray(), 2);
# 				if (sp.Length != 2) continue;
# 				var k = sp[0].Trim();
# 				var v = sp[1].Trim();
# 				dict[k] = v;
# 			}
#
# 			return dict;
# 		}
#
# 		private void SaveSettings(string file, Dictionary<string, string> dict)
# 		{
# 			List<string> lines = new List<string>();
# 			foreach (var e in dict)
# 			{
# 				lines.Add( string.Format("{0}={1}", e.Key, e.Value) );
# 			}
# 			File.WriteAllLines(file, lines.ToArray());
# 		}