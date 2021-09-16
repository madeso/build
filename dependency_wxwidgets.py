#!/usr/bin/env python3
import urllib.request
import os
import zipfile
import sys
import argparse
import re
import subprocess



###############################################################################
## Classes


class TextReplacer:
    def __init__(self):
        self.res = []

    def add(self, reg: str, rep: str):
        self.res.append( (reg, rep) )
        return self

    def replace(self, text: str) -> str:
        for replacer in self.res:
            reg = replacer[0]
            rep = replacer[1]
            text = text.replace(reg, rep)
        return text


class Settings:
    def __init__(self, root: str, install_dist: str, install: str, wx_root: str, build: str, appveyor_msbuild: str, platform: str):
        self.root = root
        self.install_dist = install_dist
        self.install = install
        self.wx_root = wx_root
        self.build = build
        self.appveyor_msbuild = appveyor_msbuild
        self.platform = platform

    def print(self):
        print('root:', self.root)
        print('install_dist:', self.install_dist)
        print('install:', self.install)
        print('wx_root:', self.wx_root)
        print('build:', self.build)
        print('appveyor_msbuild:', self.appveyor_msbuild)
        print('platform:', self.platform)

    def is_appveyor(self) -> bool:
        key = 'APPVEYOR'
        value = os.environ[key] if key in os.environ else ''
        return value.lower().strip() == 'true'

    def append_appveyor(self, args):
        if self.is_appveyor():
            args.append(self.appveyor_msbuild)


###############################################################################
## Functions


def setup() -> Settings:
    root = os.getcwd()
    install_dist = os.path.join(root, 'dependencies')
    install = os.path.join(root, 'dist')
    wx_root = os.path.join(install_dist, 'wx')
    build = os.path.join(root, 'build')
    appveyor_msbuild = r'/logger:C:\Program Files\AppVeyor\BuildAgent\Appveyor.MSBuildLogger.dll'

    platform = 'x64'
    if os.environ.get('PLATFORM', 'unknown') == 'x86':
        platform = 'Win32'

    return Settings(root, install_dist, install, wx_root, build, appveyor_msbuild, platform)


def verify_dir_exist(path):
    if not os.path.isdir(path):
        os.makedirs(path)


def download_file(url, path):
    if not os.path.isfile(path):
        urllib.request.urlretrieve(url, path)
    else:
        print("Already downloaded", path)


def list_projects_in_solution(path):
    ret = []
    directory_name = os.path.dirname(path)
    project_line = re.compile(r'Project\("[^"]+"\) = "[^"]+", "([^"]+)"')
    with open(path) as sln:
        for line in sln:
            # Project("{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}") = "richtext", "wx_richtext.vcxproj", "{7FB0902D-8579-5DCE-B883-DAF66A885005}"
            project_match = project_line.match(line)
            if project_match:
                ret.append(os.path.join(directory_name, project_match.group(1)))
    return ret


def add_definition_to_project(path, define):
    # <PreprocessorDefinitions>WIN32;_LIB;_CRT_SECURE_NO_DEPRECATE=1;_CRT_NON_CONFORMING_SWPRINTFS=1;_SCL_SECURE_NO_WARNINGS=1;__WXMSW__;NDEBUG;_UNICODE;WXBUILDING;%(PreprocessorDefinitions)</PreprocessorDefinitions>
    preproc = re.compile(r'([ ]*<PreprocessorDefinitions>)([^<]*</PreprocessorDefinitions>)')
    lines = []
    with open(path) as project:
        for line in project:
            preproc_match = preproc.match(line)
            if preproc_match:
                lines.append('{0}{1};{2}'.format(preproc_match.group(1), define, preproc_match.group(2)))
            else:
                lines.append(line.rstrip())
    with open(path, mode='w') as project:
        for line in lines:
            project.write(line + '\n')

# change from:
# <RuntimeLibrary>MultiThreadedDebugDLL</RuntimeLibrary> to <RuntimeLibrary>MultiThreadedDebug</RuntimeLibrary>
# <RuntimeLibrary>MultiThreadedDLL</RuntimeLibrary> to <RuntimeLibrary>MultiThreaded</RuntimeLibrary>
def change_to_static_link(path):
    mtdebug = re.compile(r'([ ]*)<RuntimeLibrary>MultiThreadedDebugDLL')
    mtrelease = re.compile(r'([ ]*)<RuntimeLibrary>MultiThreadedDLL')
    lines = []
    with open(path) as project:
        for line in project:
            mdebug = mtdebug.match(line)
            mrelease = mtrelease.match(line)
            if mdebug:
                print('in {project} changed to static debug'.format(project=path))
                lines.append('{spaces}<RuntimeLibrary>MultiThreadedDebug</RuntimeLibrary>'.format(spaces=mdebug.group(1)))
            elif mrelease:
                print('in {project} changed to static release'.format(project=path))
                lines.append('{spaces}<RuntimeLibrary>MultiThreaded</RuntimeLibrary>'.format(spaces=mrelease.group(1)))
            else:
                lines.append(line.rstrip())
    with open(path, mode='w') as project:
        for line in lines:
            project.write(line + '\n')


def change_all_projects_to_static(sln):
    projects = list_projects_in_solution(sln)
    for proj in projects:
        change_to_static_link(proj)


def add_definition_to_solution(sln, definition):
    projects = list_projects_in_solution(sln)
    for proj in projects:
        add_definition_to_project(proj, definition)


def make_single_project_64(project_path, rep):
    if not os.path.isfile(project_path):
        print('missing ' + project_path)
        return
    lines = []
    with open(project_path) as project:
        for line in project:
            new_line = rep.replace(line.rstrip())
            lines.append(new_line)
    with open(project_path, 'w') as project:
        for line in lines:
            project.write(line + '\n')


def make_projects_64(sln):
    projects = list_projects_in_solution(sln)
    rep = TextReplacer()
    rep.add('Win32', 'x64')
    rep.add('<DebugInformationFormat>EditAndContinue</DebugInformationFormat>', '<DebugInformationFormat>ProgramDatabase</DebugInformationFormat>')
    rep.add('<TargetMachine>MachineX86</TargetMachine>', '<TargetMachine>MachineX64</TargetMachine>')
    # protobuf specific hack since cmake looks in x64 folder
    rep.add(r'<OutDir>Release\</OutDir>', r'<OutDir>x64\Release\</OutDir>')
    rep.add(r'<OutDir>Debug\</OutDir>', r'<OutDir>x64\Debug\</OutDir>')
    for project in projects:
        make_single_project_64(project, rep)


def make_solution_64(solution_path):
    rep = TextReplacer()
    rep.add('Win32', 'x64')

    lines = []
    with open(solution_path) as slnlines:
        for line in slnlines:
            new_line = rep.replace(line.rstrip())
            lines.append(new_line)

    with open(solution_path, 'w') as solution_handle:
        for line in lines:
            solution_handle.write(line + '\n')


def convert_sln_to_64(sln):
    make_solution_64(sln)
    make_projects_64(sln)


def extract_zip_to(path_to_zip, target):
    with zipfile.ZipFile(path_to_zip, 'r') as zip_handle:
        zip_handle.extractall(target)



###############################################################################
## Commands


def handle_make_solution_64_cmd(args):
    convert_sln_to_64(args.sln)


def handle_change_all_projects_to_static_cmd(args):
    change_all_projects_to_static(args.sln)


def handle_list_projects_cmd(cmd):
    projects = list_projects_in_solution(cmd.sln)
    for proj in projects:
        print("project", proj)


def handle_add_definition_cmd(args):
    add_definition_to_project(args.project, args.define)


def handle_change_to_static_cmd(args):
    change_to_static_link(args.project)


def handle_install_cmd(args):
    settings = setup()

    build = args.build

    wx_url = "https://github.com/wxWidgets/wxWidgets/releases/download/v3.1.4/wxWidgets-3.1.4.zip"
    wx_zip = os.path.join(settings.install_dist, "wx.zip")
    wx_sln = os.path.join(settings.wx_root, 'build', 'msw', 'wx_vc16.sln')

    print('Root:', settings.root)
    print('wxWidgets solution: ', wx_sln)

    verify_dir_exist(settings.install_dist)
    verify_dir_exist(settings.wx_root)

    print("downloading wx...")
    download_file(wx_url, os.path.join(settings.install_dist, wx_zip))

    print("extracting wx")
    extract_zip_to(wx_zip, settings.wx_root)

    print("changing wx to static")
    change_all_projects_to_static(wx_sln)

    print("building wxwidgets")
    print("-----------------------------------")

    wx_msbuild_cmd = [
        'msbuild',
        '/p:Configuration=Release',
        '/p:Platform={}'.format(settings.platform)
    ]
    settings.append_appveyor(wx_msbuild_cmd)
    wx_msbuild_cmd.append(wx_sln)

    if build:
        sys.stdout.flush()
        subprocess.check_call(wx_msbuild_cmd)
    else:
        print(wx_msbuild_cmd)


def handle_cmake_cmd(_):
    settings = setup()

    subinstall = os.path.join(settings.install, 'windows', settings.platform)
    os.makedirs(settings.build)
    os.makedirs(settings.install)
    os.makedirs(subinstall)

    generator = 'Visual Studio 16 2019'

    cmakecmd = [
        'cmake',
        "-DCMAKE_INSTALL_PREFIX={}".format(subinstall),
        "-DwxWidgets_ROOT_DIR={}".format(settings.wx_root),
        "-DRIDE_BUILD_COMMIT=%APPVEYOR_REPO_COMMIT%",
        "-DRIDE_BUILD_NUMBER=%APPVEYOR_BUILD_NUMBER%",
        "-DRIDE_BUILD_BRANCH=%APPVEYOR_REPO_BRANCH%",
        "-DRIDE_BUILD_REPO=%APPVEYOR_REPO_NAME%",
        '-G', generator,
        '-A', settings.platform,
        settings.root
    ]
    sys.stdout.flush()
    subprocess.check_call(cmakecmd, cwd=settings.build)


def handle_build_cmd(_):
    settings = setup()

    ride_sln = os.path.join(settings.build, 'PACKAGE.vcxproj')
    ride_msbuild_cmd = [
        'msbuild',
        '/p:Configuration=Release',
        '/p:Platform={}'.format(settings.platform),
        settings.appveyor_msbuild,
        ride_sln
    ]

    sys.stdout.flush()
    subprocess.check_call(ride_msbuild_cmd)


def handle_print_cmd(_):
    settings = setup()
    settings.print()



###############################################################################
## Main


def main():
    parser = argparse.ArgumentParser(description='Does the windows build')
    subparsers = parser.add_subparsers()

    install_parser = subparsers.add_parser('install')
    install_parser.set_defaults(func=handle_install_cmd)
    install_parser.add_argument('--nobuild', dest='build', action='store_const', const=False, default=True)

    install_parser = subparsers.add_parser('listprojects')
    install_parser.set_defaults(func=handle_list_projects_cmd)
    install_parser.add_argument('sln', help='solution file')

    static_project_parser = subparsers.add_parser('static_project')
    static_project_parser.set_defaults(func=handle_change_to_static_cmd)
    static_project_parser.add_argument('project', help='make a project staticly link to the CRT')

    static_project_parser = subparsers.add_parser('to64')
    static_project_parser.set_defaults(func=handle_make_solution_64_cmd)
    static_project_parser.add_argument('sln', help='the solution to upgrade')

    static_solution_parser = subparsers.add_parser('static_sln')
    static_solution_parser.set_defaults(func=handle_change_all_projects_to_static_cmd)
    static_solution_parser.add_argument('sln', help='make all the projects in the specified solution staticly link to the CRT')

    install_parser = subparsers.add_parser('add_define')
    install_parser.set_defaults(func=handle_add_definition_cmd)
    install_parser.add_argument('project', help='project file')
    install_parser.add_argument('define', help='preprocessor to add')

    cmake_parser = subparsers.add_parser('cmake')
    cmake_parser.set_defaults(func=handle_cmake_cmd)

    build_parser = subparsers.add_parser('build')
    build_parser.set_defaults(func=handle_build_cmd)

    print_parser = subparsers.add_parser('print')
    print_parser.set_defaults(func=handle_print_cmd)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
