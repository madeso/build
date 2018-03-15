#!/usr/bin/env python3

import subprocess
import os
import sys
import winreg
import argparse
import typing
import enum
import shutil

class FoundStudio:
    def __init__(self, name: str, vcvars: str, root:str):
        self.name = name
        self.root = root
        self.vcvars = vcvars

    def __str__(self):
        return '{0} at {1}'.format(self.name, self.vcvars)


@enum.unique
class Platform(enum.Enum):
    WIN32 = 0
    X64 = 1

    def __str__(self):
        if self == Platform.WIN32:
            return 'win32'
        elif self == Platform.X64:
            return 'win64'
        else:
            return '<{0}>'.format(self.name)


def get_visual_studio_name(p: Platform)->str:
    if p == Platform.WIN32:
        return 'x86'
    elif p == Platform.X64:
        return 'amd64'
    else:
        print('Unknown value of platform type: {0}'.format(p))
        return 'unknown-platform'


def run_command(mywd: str, mycmd: str):
    try:
        retcode = subprocess.call(mycmd, cwd=mywd, shell=True)
        if retcode < 0:
            print("Child was terminated by signal", -retcode, file=sys.stderr)
    except OSError as e:
        print("Execution failed:", e)


def get_working_folder() -> str:
    cwd = os.getcwd()
    wd = os.path.join(cwd, 'libxml2', 'win32')
    return wd


def is_valid_working_folder(wd: str) -> bool:
    return os.path.isdir(wd)


def print_invalid_structure(wd: str):
    print("ERROR: libxml2 structure NOT VALID!")
    print("The layout should be:")
    print("  libxml.py")
    print("+ libxml2")
    print("|- doc")
    print("|- include")
    print("|- win32")
    print()
    print("That is win32 folder should exist at", wd)


VCVARS = "..\\..\\VC\\vcvarsall.bat" # need to go 2 folders back rom the "install dir"


def parent_folder(p: str) -> str:
    s = os.path.split(p)
    if s[1] == '':
        # if the right of split is empty, the path ended with a / and we should do one more split/dirname
        return os.path.dirname(s[0])
    else:
        return s[0]


def find_installed_studios() -> typing.List[FoundStudio]:
    studios: typing.List[FoundStudio] = []
    local_machine = winreg.ConnectRegistry(None, winreg.HKEY_LOCAL_MACHINE)
    visual_studio_root_key = winreg.OpenKey(local_machine, r"SOFTWARE\Microsoft\VisualStudio")
    for i in range(1024):
        try:
            visual_studio_key_name = winreg.EnumKey(visual_studio_root_key, i)
            visual_studio_key = winreg.OpenKey(visual_studio_root_key, visual_studio_key_name)
            install_dir = winreg.QueryValueEx(visual_studio_key, "InstallDir")[0]
            solution_root = parent_folder(parent_folder(install_dir))
            vcvars_path = os.path.join(solution_root, 'VC', 'vcvarsall.bat')
            if os.path.isfile(vcvars_path):
                studios.append(FoundStudio(visual_studio_key_name, vcvars_path, solution_root))
        except WindowsError:
            # break
            # we get a windows error before we are done, keep going!
            pass
    return studios


def validate_log_sub(path: str, text: str, fails: bool):
    with open(path, "r") as f:
        for line in f:
            if text in line:
                return not fails
    return fails


def validate_log(path: str, text: str, fails: bool, name: str):
    if validate_log_sub(path, text, fails):
        print('{} ok.'.format(name.capitalize()))
    else:
        print('{} FAILED!!!!!!'.format(name.upper()), file=sys.stderr)


class BuildCommand:
    def __init__(self, description: str, build_bat_path: str, vcvars_command: str, configure_command: str, make_command: str, working_directory: str, build_folder: str, vcvars_log: str, configure_log: str, make_log: str):
        self.description = description
        self.build_bat_path = build_bat_path
        self.vcvars_command = vcvars_command
        self.configure_command = configure_command
        self.make_command = make_command
        self.working_directory = working_directory
        self.build_folder = build_folder
        self.vcvars_log = vcvars_log
        self.configure_log = configure_log
        self.make_log = make_log

    def build(self):
        lines = [
            '@echo Setting up visual studio',
            self.vcvars_command,
            '@echo Configuring build',
            self.configure_command,
            '@echo Building....',
            self.make_command,
            '@echo deleting shared makefile',
            '@del Makefile'
        ]
        with open(self.build_bat_path, "w") as f:
            for l in lines:
                f.write(l + '\n')
        run_command(self.working_directory, self.build_bat_path)
        validate_log(self.configure_log, 'Created Makefile.', False, 'configure')
        validate_log(self.make_log, 'error ', True, 'build')

    def clean(self):
        if os.path.isfile(self.build_bat_path):
            os.remove(self.build_bat_path)
        shutil.rmtree(self.build_folder, ignore_errors=True)
        parent = parent_folder(self.build_folder)
        if os.path.isdir(parent):
            if not os.listdir(parent):
                os.removedirs(parent)


def create_build(studio: FoundStudio, platform: Platform, is_debug: bool, working_directory: str) -> BuildCommand:
    studio_name = os.path.basename(studio.root).strip()
    studio_name_fixed = studio_name.replace(".", "").replace(" ", "_")
    configuration = "debug" if is_debug else "release"
    description = '{} {} {}'.format(studio_name, configuration, platform)
    debug = "debug=yes" if is_debug else "debug=no"
    build_folder_relative_path = "..\\..\\build\\" + studio_name_fixed + "\\" + str(platform) + "-" + configuration
    build_folder = os.path.normpath(os.path.join(working_directory, build_folder_relative_path))
    if not os.path.isdir(build_folder):
        os.makedirs(build_folder)
    vcvars_log = os.path.join(build_folder, "vcvars.log")
    vcvars_command = '@call "{studio}" {platform} 1>"{log}" 2>&1'\
        .format(studio=studio.vcvars,
                platform=get_visual_studio_name(platform),
                log=vcvars_log
                )
    configure_log = os.path.join(build_folder, "gen.log")
    configure_command = '@cscript configure.js {debug} {common_settings} prefix="{build}" 1>"{log}" 2>&1'\
        .format(debug=debug,
                common_settings='ftp=no http=no compiler=msvc static=yes vcmanifest=yes iconv=no zlib=no',
                build=build_folder,
                log=configure_log
                )
    make_log = os.path.join(build_folder, "build.log")
    make_command = '@nmake.exe /f makefile.msvc rebuild install 1>"{log}" 2>&1'\
        .format(log=make_log)
    bat_name = "build-{}-{}-{}.bat".format(studio_name_fixed, configuration, platform)
    build_bat_path = os.path.join(working_directory, bat_name)
    return BuildCommand(description, build_bat_path, vcvars_command, configure_command, make_command, working_directory, build_folder, vcvars_log, configure_log, make_log)


def loop_all(studios: typing.List[FoundStudio], callback: typing.Callable[[BuildCommand], None]):
    if len(studios) == 0:
        print('No studios found, aborting', file=sys.stderr)
        return

    working_directory = get_working_folder()
    if not is_valid_working_folder(working_directory):
        print_invalid_structure(working_directory)
        return

    for s in studios:
        for platform in list(Platform):
            for is_debug in [True, False]:
                build = create_build(s, platform, is_debug, working_directory)
                callback(build)


def please_clean(studios: typing.List[FoundStudio]):
    def callback(build: BuildCommand):
        build.clean()
    loop_all(studios, callback)


def please_build(studios: typing.List[FoundStudio]):
    def callback(build: BuildCommand):
        print('Building ', build.description)
        build.build()
        print()
    loop_all(studios, callback)


def select_studios(all_studios: typing.List[FoundStudio], studios: typing.List[str]) -> typing.List[FoundStudio]:
    selected_studios: typing.List[FoundStudio] = []
    ok = True
    for studio_name in studios:
        s = [s for s in all_studios if s.name == studio_name]
        if len(s) == 0:
            print('ERROR: {0} doesnt seems to be a valid studio'.format(studio_name), file=sys.stderr)
            ok = False
        elif len(s) == 1:
            selected_studios.append(s[0])
        else:
            print('ERROR: Found to many studios in search', file=sys.stderr)
            ok = False

    if ok:
        return selected_studios
    else:
        return []


def handle_list_studios(args):
    studios = find_installed_studios()
    print('Detected visual studios:')
    if len(studios) == 0:
        print('No studios found.')
    for s in studios:
        print('\t{0}: {1}'.format(s.name, s.vcvars))


def handle_clean_all_libxml(args):
    studios = find_installed_studios()
    please_clean(studios)


def handle_clean_specific_libxml(args):
    all_studios = find_installed_studios()
    selected_studios = select_studios(all_studios, args.studios)
    please_clean(selected_studios)


def handle_build_all_libxml(args):
    studios = find_installed_studios()
    please_build(studios)


def handle_build_specific_libxml(args):
    all_studios = find_installed_studios()
    selected_studios = select_studios(all_studios, args.studios)
    please_build(selected_studios)


def main():
    parser = argparse.ArgumentParser(description='Utility for building libxml2.')
    parser.set_defaults(func=None)
    parser.set_defaults(parser=parser)
    subparsers = parser.add_subparsers()

    list_parser = subparsers.add_parser('vs', help='list detected visual studios')
    list_parser.set_defaults(func=handle_list_studios)

    ############################################################################

    clean_parser = subparsers.add_parser('clean', help='clean build files')
    clean_parser.set_defaults(parser=clean_parser)
    clean_subparsers = clean_parser.add_subparsers()

    clean_all_parser = clean_subparsers.add_parser('all', help='clean libxml2 with all studios')
    clean_all_parser.set_defaults(func=handle_clean_all_libxml)

    clean_specific_parser = clean_subparsers.add_parser('specific', help='clean libxml2 with specific studios')
    clean_specific_parser.add_argument('studios', metavar='S', type=str, nargs='+', help='the studios to clean')
    clean_specific_parser.set_defaults(func=handle_clean_specific_libxml)

    ############################################################################

    build_parser = subparsers.add_parser('build', help='build libxml2')
    build_parser.set_defaults(parser=build_parser)
    build_subparsers = build_parser.add_subparsers()

    build_all_parser = build_subparsers.add_parser('all', help='build libxml2 with all studios')
    build_all_parser.set_defaults(func=handle_build_all_libxml)

    build_specific_parser = build_subparsers.add_parser('specific', help='build libxml2 with specific studios')
    build_specific_parser.add_argument('studios', metavar='S', type=str, nargs='+', help='the studios to build')
    build_specific_parser.set_defaults(func=handle_build_specific_libxml)

    ############################################################################

    args = parser.parse_args()

    if args.func is None:
        print('ERROR: No command specified.', file=sys.stderr)
        args.parser.print_help()
    else:
        args.func(args)

if __name__ == "__main__":
    main()
