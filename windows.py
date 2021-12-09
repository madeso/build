#!/usr/bin/env python3
"""build script for windows for ride"""

import os
import subprocess
import argparse
import typing
import json
from collections.abc import Callable

import buildtools.core as btcore
import buildtools.deps as btdeps
import buildtools.cmake as btcmake
import buildtools.args as btargs
import buildtools.visualstudio as btstudio


def default_or_required_string(arg, to_string):
    if arg is None:
        return '<required>'
    else:
        return f'({to_string(arg)})'

class BuildEnviroment:
    def __init__(self, compiler : typing.Optional[btargs.Compiler], platform : typing.Optional[btargs.Platform]):
        self.compiler = compiler
        self.platform = platform
    
    def get_generator(self) -> btcmake.Generator:
        return btstudio.visual_studio_generator(self.compiler, self.platform)
    
    def save_to_file(self, path: str):
        """save the build environment to a json file"""
        data = {}
        data['compiler'] = self.compiler.name
        data['platform'] = self.platform.name
        with open(path, 'w') as f:
            json.dump(data, f, indent=4)
    
    def add_options(self, parser: argparse.ArgumentParser):
        """add the build environment to an argparse parser"""
        parser.add_argument('--compiler', type=str.lower, default=None, help=f'compiler to use {default_or_required_string(self.compiler, btargs.compiler_to_string)}', choices=btargs.all_compiler_names(), required=self.compiler is None)
        parser.add_argument('--platform', type=str.lower, default=None, help=f'platform to use {default_or_required_string(self.platform, btargs.platform_to_string)}', choices=btargs.all_platform_names(), required=self.platform is None)
        parser.add_argument('--force', action='store_true', help='force the compiler and platform to be changed')

    def update_from_args(self, args: argparse.Namespace):
        """update the build environment from an argparse namespace"""
        failure = False
        if args.compiler is not None:
            new_compiler = btargs.compiler_from_name(args.compiler, True)
            if self.compiler is not None and self.compiler != new_compiler:
                if args.force:
                    print(f'WARNING: Compiler changed via argument from {btargs.compiler_to_string(self.compiler)} to {btargs.compiler_to_string(new_compiler)}')
                    self.compiler = new_compiler
                else:
                    print(f'ERROR: Compiler changed via argument from {btargs.compiler_to_string(self.compiler)} to {btargs.compiler_to_string(new_compiler)}')
                    failure = True
            else:
                self.compiler = new_compiler
        if args.platform is not None:
            new_platform = btargs.platform_from_name(args.platform, True)
            if self.platform is not None and self.platform != new_platform:
                if args.force:
                    print(f'WARNING: Platform changed via argument from {btargs.platform_to_string(self.platform)} to {btargs.platform_to_string(new_platform)}')
                    self.platform = new_platform
                else:
                    print(f'ERROR: Platform changed via argument from {btargs.platform_to_string(self.platform)} to {btargs.platform_to_string(new_platform)}')
                    failure = True
            else:
                self.platform = new_platform
        
        if failure:
            print('ERROR: Build environment is invalid')
            exit(-2)
    
    def validate(self):
        """validate the build environment"""
        status = True
        if self.compiler is None:
            print('ERROR: Compiler not set')
            status = False
        if self.platform is None:
            print('ERROR: Platform not set')
            status = False
        return status

    def exit_if_invalid(self):
        """exit if the build environment is invalid"""
        if not self.validate():
            exit(-2)

def load_build_from_file(path: str, print_error: bool) -> BuildEnviroment:
    """load build enviroment from json file"""
    if btcore.file_exists(path):
        with open(path, 'r') as file:
            data = json.load(file)
            compiler_name = data['compiler']
            platform_name = data['platform']
            return BuildEnviroment(btargs.compiler_from_name(compiler_name, print_error), btargs.platform_from_name(platform_name, print_error))
    else:
        return BuildEnviroment(None, None)


class Dependency:
    def __init__(self, name: str, add_cmake_arguments_impl: Callable[[btcmake.CMake, BuildEnviroment], None], install_impl: Callable[[BuildEnviroment], None], status_impl: Callable[[], typing.List[str]]):
        self.name = name
        self.add_cmake_arguments_impl = add_cmake_arguments_impl
        self.install_impl = install_impl
        self.status_impl = status_impl

    def add_cmake_arguments(self, cmake: btcmake.CMake, env: BuildEnviroment):
        self.add_cmake_arguments_impl(cmake, env)

    def install(self, env: BuildEnviroment):
        self.install_impl(env)

    def status(self) -> typing.List[str]:
        """get the status of the dependency"""
        return self.status_impl()


class Data:
    """data for the build"""
    def __init__(self, name: str, root_dir: str):
        self.dependencies = []
        self.name = name
        self.root_dir = root_dir
        self.build_base_dir = os.path.join(root_dir, "build")
        self.build_dir = os.path.join(root_dir, "build", name)
        self.dependency_dir = os.path.join(root_dir, "build", "deps")

    def get_path_to_settings(self) -> str:
        """get the path to the settings file"""
        return os.path.join(self.build_base_dir, "settings.json")
    
    def load_build(self, print_error: bool) -> BuildEnviroment:
        """load the build environment from the settings file"""
        return load_build_from_file(self.get_path_to_settings(), print_error)
    
    def add_dependency(self, dep: Dependency):
        """add a dependency"""
        self.dependencies.append(dep)


def default_data(name: str) -> Data:
    """default data"""
    return Data(name, os.getcwd())

def save_build(build: BuildEnviroment, data: Data):
    """save the build environment to the settings file"""
    os.makedirs(data.build_base_dir, exist_ok=True)
    build.save_to_file(data.get_path_to_settings())

###############################################################################

def add_dependency_sdl2(data: Data):
    """add sdl2 dependency"""
    root_folder = os.path.join(data.dependency_dir, 'sdl2')
    build_folder = os.path.join(root_folder, 'cmake-build')

    def add_sdl_arguments(cmake: btcmake.CMake, env: BuildEnviroment):
        cmake.add_argument('SDL2_HINT_ROOT', root_folder)
        cmake.add_argument('SDL2_HINT_BUILD', build_folder)
    
    def install_sdl_dependency(build: BuildEnviroment):
        btdeps.install_dependency_sdl2(data.dependency_dir, root_folder, build_folder, build.get_generator())

    def status_sdl_dependency() -> typing.List[str]:
        return [f'Root: {root_folder}', f'Build: {build_folder}']

    d = Dependency('sdl2', add_sdl_arguments, install_sdl_dependency, status_sdl_dependency)
    data.add_dependency(d)


def add_dependency_python(data: Data):
    """add python dependency"""

    def add_python_arguments(cmake: btcmake.CMake, env: BuildEnviroment):
        if 'PYTHON' in os.environ:
            python_exe = os.environ['PYTHON']+'\\python.exe'
            project.add_argument('PYTHON_EXECUTABLE:FILEPATH', python_exe)

    def install_python_dependency(build: BuildEnviroment):
        pass

    def status_python_dependency() -> typing.List[str]:
        return []

    d = Dependency('python', add_python_arguments, install_python_dependency, status_python_dependency)
    data.add_dependency(d)


def add_dependency_assimp(data: Data):
    """add assimp dependency"""
    assimp_folder = os.path.join(data.dependency_dir, 'assimp')
    assimp_install_folder = os.path.join(assimp_folder, 'cmake-install')

    def add_assimp_arguments(cmake: btcmake.CMake, env: BuildEnviroment):
        cmake.add_argument('ASSIMP_ROOT_DIR', assimp_install_folder)
    
    def install_assimp_dependency(build: BuildEnviroment):
        btdeps.install_dependency_assimp(data.dependency_dir, assimp_folder, assimp_install_folder, build.get_generator())

    def status_assimp_dependency() -> typing.List[str]:
        return [f'Root: {assimp_folder}', f'Install: {assimp_install_folder}']

    d = Dependency('assimp', add_assimp_arguments, install_assimp_dependency, status_assimp_dependency)
    data.add_dependency(d)

###############################################################################


def generate_cmake_project(build: BuildEnviroment, data: Data) -> btcmake.CMake:
    """generate the ride project"""
    project = btcmake.CMake(data.build_dir, data.root_dir, build.get_generator())

    for dep in data.dependencies:
        dep.add_cmake_arguments(project, build)

    return project


def run_install(build: BuildEnviroment, data: Data):
    """install dependencies"""
    for dep in data.dependencies:
        dep.install(build)


def run_cmake(build: BuildEnviroment, data: Data, only_print: bool):
    """configure the euphoria cmake project"""
    generate_cmake_project(build, data).config(only_print)


def run(args) -> str:
    """run a terminal and return the output or error"""
    try:
        return subprocess.check_output(args, stderr=subprocess.STDOUT)
    except subprocess.CalledProcessError as error:
        print('Failed to run {} {}'.format(error.cmd, error.returncode))
        return error.stdout


###############################################################################


def on_cmd_install(arg, data: Data):
    """callback for install command"""
    build = data.load_build(True)
    build.update_from_args(arg)
    build.exit_if_invalid()
    save_build(build, data)

    run_install(build, data)


def on_cmd_cmake(arg, data: Data):
    """callback for cmake command"""
    build = data.load_build(True)
    build.update_from_args(arg)
    build.exit_if_invalid()
    save_build(build, data)

    run_cmake(build, data, arg.print)


def on_cmd_dev(arg, data: Data):
    """callback for dev command"""
    build = data.load_build(True)
    build.update_from_args(arg)
    build.exit_if_invalid()
    save_build(build, data)

    run_install(build, data)
    run_cmake(build, data, False)


def on_cmd_build(arg, data: Data):
    """callback for build cmd"""
    build = data.load_build(True)
    build.update_from_args(arg)
    build.exit_if_invalid()
    save_build(build, data)

    generate_cmake_project(build, data).build()


def on_status_build(args, data: Data):
    """callback for status build cmd"""
    build = data.load_build(True)
    build.update_from_args(args)
    build.exit_if_invalid()

    print(f'Project: {data.name}')
    print()
    print(f'Data: {data.get_path_to_settings()}')
    print(f'Root: {data.root_dir}')
    print(f'Build: {data.build_dir}')
    print(f'Dependencies: {data.dependency_dir}')
    indent = ' ' * 4
    for dep in data.dependencies:
        print(f'{indent}{dep.name}')
        lines = dep.status()
        for line in lines:
            print(f'{indent*2}{line}')
    print()
    print(f'Compiler: {btargs.compiler_to_string(build.compiler)}')
    print(f'Platform: {btargs.platform_to_string(build.platform)}')
    gen = build.get_generator()
    print(f'CMake generator: {gen.generator}')
    if gen.arch is not None:
        print(f'CMake archictecture: {gen.arch}')
    print()
    

###############################################################################


def main(data: Data):
    """entry point for script"""
    parser = argparse.ArgumentParser(description='Does the windows build')
    parser.set_defaults(func=None)
    subparsers = parser.add_subparsers()

    build = data.load_build(False)

    install_parser = subparsers.add_parser('install', help='install dependencies')
    install_parser.set_defaults(func=on_cmd_install)
    build.add_options(install_parser)

    cmmake_parser = subparsers.add_parser('cmake', help='configure cmake project')
    cmmake_parser.add_argument('--print', action='store_true')
    cmmake_parser.set_defaults(func=on_cmd_cmake)
    build.add_options(cmmake_parser)

    dev_parser = subparsers.add_parser('dev', help='dev is install+cmake')
    dev_parser.set_defaults(func=on_cmd_dev)
    build.add_options(dev_parser)

    build_parser = subparsers.add_parser('build', help='build the project')
    build_parser.set_defaults(func=on_cmd_build)
    build.add_options(build_parser)

    status_parser = subparsers.add_parser('stat', help='print the status of the build')
    status_parser.set_defaults(func=on_status_build)
    build.add_options(status_parser)

    arg = parser.parse_args()
    if arg.func is None:
        parser.print_help()
    else:
        arg.func(arg, data)
