#!/usr/bin/env python3

import os
import argparse
import collections
import typing
import itertools
import xml.etree.ElementTree as ET
import difflib

def find_file(filename: str, include_dirs: list = []) -> typing.Optional[str]:
    for include_dir in include_dirs:
        full_path = os.path.normpath(os.path.join(include_dir, filename))
        if os.path.isfile(full_path):
            return full_path
    return None


class Statement:
    pass

class Command(Statement):
    def __init__(self, name: str, value: str):
        self.name = name
        self.value = value


class Block(Statement):
    def __init__(self, name: str, condition: str, true_block: typing.List[Statement], false_block: typing.List[Statement]):
        self.name = name
        self.condition = condition
        self.true_block = true_block
        self.false_block = false_block

class Commands:
    def __init__(self, commands: typing.Iterable[Command]):
        self.commands = list(commands)
        self.index = 0

    def validate_index(self):
        if self.index < 0 or self.index >= len(self.commands):
            return False
        return True

    def expect_valid_index(self):
        if not self.validate_index():
            raise Exception(f"Invalid index {self.index} {len(self.commands)}")

    def peek(self) -> Command:
        self.expect_validate_index()
        return self.commands[self.index]
    
    def opeek(self) -> typing.Optional[Command]:
        if self.validate_index():
            return self.commands[self.index]
        return None
    
    def skip(self):
        self.index += 1
    
    def undo(self):
        self.index -= 1
    
    def iter(self):
        while self.index < len(self.commands):
            self.expect_valid_index()
            c = self.commands[self.index]
            self.index += 1
            yield c


def is_if_start(name: str) -> bool:
    return name == 'if' or name == 'ifdef' or name == 'ifndef'


def peek_name(commands: Commands) -> str:
    p = commands.opeek()
    if p:
        return p.name
    return ''

def group_commands(commands: Commands, depth: int) -> typing.Iterable[Statement]:
    for command in commands.iter():
        if is_if_start(command.name):
            group = Block(command.name, command.value, [], [])
            group.true_block = list(group_commands(commands, depth+1))
            if peek_name(commands) == 'else':
                commands.skip()
                group.false_block = list(group_commands(commands, depth+1))
            if peek_name(commands) == 'endif':
                commands.skip()
        elif command.name == 'else':
            commands.undo()
            break
        elif command.name == 'endif':
            if depth > 0:
                commands.undo()
                break
            else:
                print('Ignored unmatched endif')
        else:
            yield command


def parse_to_statements(lines: typing.Iterable[str]) -> typing.List[Statement]:
    only_commands = filter(lambda ls: ls.startswith('#'), lines)
    cmd = (ls.strip('#').strip().split(' ', maxsplit=1) for ls in only_commands)
    commands = Commands(Command(p[0], p[1] if len(p)>1 else '') for p in cmd)
    return list(group_commands(commands, 0))


def is_pragma_once(command: Command) -> bool:
    if command.name != 'pragma':
        return False
    args = command.value.split(' ', maxsplit=1)
    return args[0] == 'once'


class State:
    def __init__(self, on_not_found, on_found, include_dirs: typing.List[str] = [], defines: typing.Dict[str, str] = {}):
        self.defines = defines
        self.include_dirs_without_current = include_dirs
        self.on_not_found = on_not_found
        self.on_found = on_found
    
    def on_statement(self, command: Command, include_dirs: typing.List[str], depth: int, filename: str):
        if command.name == 'pragma':
            self.on_pragma(command, include_dirs, depth, filename)
        elif command.name == 'define':
            self.on_define(command, include_dirs, depth, filename)
        elif command.name == 'include':
            self.on_include(command, include_dirs, depth, filename)
        else:
            print(f'{indent_for_depth(depth)}unknown command: {command.name}')
    
    def on_pragma(self, command: Command, include_dirs: typing.List[str], depth: int, filename: str):
        if is_pragma_once(command):
            return
        else:
            print(f'{indent_for_depth(depth)}unknown pragma: {command.value}')
    
    def on_define(self, command: Command, include_dirs: typing.List[str], depth: int, filename: str):
        args = command.value.split(' ')
        self.defines[args[0]] = args[1] if len(args) > 1 else ''
    
    def on_include(self, command: Command, include_dirs: typing.List[str], depth: int, filename: str):
        relative_file_name = command.value.split(' ')[0].strip()[1:-1].strip()

        full_path = find_file(relative_file_name, include_dirs)

        if full_path is None:
            self.on_not_found(relative_file_name, filename, depth)
        else:
            self.on_found(full_path, filename, depth)
            self.list_headers(full_path, depth + 1)
    
    def list_headers(self, filename: str, depth: int):
        folder = os.path.dirname(filename)
        include_dirs = [folder] + self.include_dirs_without_current
        with open(filename, 'r') as f:
            statements = parse_to_statements(line.lstrip() for line in f)
            self.process_statements(statements, include_dirs, depth, filename)

    def process_statements(self, statements: typing.List[Statement], include_dirs: typing.List[str], depth: int, filename: str):
        for statement in statements:
            if isinstance(statement, Command):
                self.on_statement(statement, include_dirs, depth, filename)
            elif isinstance(statement, Block):
                is_true = False
                
                if statement.name == 'ifdef':
                   is_true = statement.condition in self.defines 
                elif statement.name == 'ifndef':
                    is_true = statement.condition not in self.defines 
                else:
                    raise Exception(f'{indent_for_depth(depth)}unhandled #if argument: {statement.confition}')
                    is_true = True
                
                if is_true:
                    self.process_statements(statement.true_block, include_dirs, depth, filename)
                else:
                    self.process_statements(statement.false_block, include_dirs, depth, filename)


def list_include_headers(filename: str, arg_include_dirs: list, on_not_found, on_found, defines: typing.Dict[str, str]):
    state = State(on_not_found, on_found, arg_include_dirs, defines)
    state.list_headers(filename, 0)
            


class Stat:
    def __init__(self):
        self.count = 0
        self.num_found = 0
        self.num_not_found = 0

    def add(stat, found: bool):
        stat.count = stat.count + 1
        
        if found:
            stat.num_found += 1
        else:
            stat.num_not_found += 1

        if stat.count % 100 == 0:
            print(f'{stat.count} files checked, found: {stat.num_found} and not: {stat.num_not_found}')


def collect_include_headers(filename: str, include_dirs: typing.List[str], defines: typing.Dict[str, str]) -> collections.Counter:
    files = collections.Counter()

    stat = Stat()
    
    def on_not_found(relative_file_name, filename, depth):
        stat.add(found=False)
        files[relative_file_name] += 1
    
    def on_found(full_path, filename, depth):
        stat.add(found=True)
        files[full_path] += 1
    
    list_include_headers(filename, include_dirs, on_not_found, on_found, defines)
    return files


def elements_named(root, name):
    for child in root:
        if child.tag == '{http://schemas.microsoft.com/developer/msbuild/2003}'+name:
            yield child


def find_include_dirs_in_vcxproj_config(config) -> typing.Iterable[str]:
    for compiles in elements_named(config, 'ClCompile'):
        for dirs in elements_named(compiles, 'AdditionalIncludeDirectories'):
            for dir in dirs.text.split(';'):
                yield dir.strip()


def find_preproc_in_vcxproj_config(config) -> typing.Iterable[typing.Tuple[str, str]]:
    for compiles in elements_named(config, 'ClCompile'):
        for dirs in elements_named(compiles, 'PreprocessorDefinitions'):
            for dir in dirs.text.split(';'):
                line = dir.strip().split('=', maxsplit=1)
                if len(line) == 2:
                    yield line[0].strip(), line[1].strip()
                else:
                    yield line[0].strip(), ''




class Config:
    def __init__(self, name: str, include_dirs: typing.List[str], defines: typing.Dict[str, str]):
        self.name = name
        self.include_dirs = include_dirs
        self.defines = defines


class ProjectFile:
    def __init__(self):
        self.configs = []
        self.include_files = []
        self.source_files = []


def find_files_in_group(group, name: str) -> typing.List[str]:
    return [file.attrib['Include'] for file in elements_named(group, name)]


def find_files_in_vcxproj(filename: str) -> ProjectFile:
    ret = ProjectFile()
    root = ET.parse(filename).getroot()
    for config in elements_named(root, 'ItemDefinitionGroup'):
        condition = config.get('Condition')
        eq = condition.find("==")
        if eq is None:
            continue
        value_expression = condition[eq+2:]
        name = value_expression.strip("'").strip()
        dirs = list(find_include_dirs_in_vcxproj_config(config))
        defines = {k:v for k,v in find_preproc_in_vcxproj_config(config)}
        ret.configs.append(Config(name, dirs, defines))
    
    for group in elements_named(root, 'ItemGroup'):
        ret.source_files += find_files_in_group(group, 'ClCompile')
        ret.include_files += find_files_in_group(group, 'ClInclude')

    return ret


def exclude_files(counts, folders: typing.Iterable[str]):
    for folder in folders:
        to_remove = []
        for f in counts:
            if f.startswith(folder):
                to_remove.append(f)
        for f in to_remove:
            counts.pop(f)


###############################################################################

def indent_for_depth(depth: int) -> str:
    return ' ' * (depth * 4)


def print_not_found(relative_file_name, filename, depth):
    print('{}missing {}'.format(indent_for_depth(depth+1), relative_file_name))


def print_found(full_path, filename, depth):
    print('{}{}'.format(indent_for_depth(depth+1), full_path))


def handle_file(args):
    abs_path = os.path.abspath(args.filename)

    print()
    print(abs_path)
    list_include_headers(abs_path, [], print_not_found, print_found, {})
    print()


def handle_project_file(args):
    project = find_files_in_vcxproj(args.filename)

    for config in project.configs:
        print(f'{config.name}:')
        print(f'{indent_for_depth(1)}Includes:')
        for include_dir in config.include_dirs:
            print(f'{indent_for_depth(2)}{include_dir}')
        print()
        print(f'{indent_for_depth(1)}Defines:')
        for k,v in config.defines.items():
            print(f'{indent_for_depth(2)}{k}: {v}')
        print()
    
    print('include files:')
    for include_file in project.include_files:
        print(f'{indent_for_depth(1)}{include_file}')
    print()
    
    print('source files:')
    for source_file in project.source_files:
        print(f'{indent_for_depth(1)}{source_file}')
    print()


def handle_file_in_project(args):
    project_file = os.path.abspath(args.project)
    project = find_files_in_vcxproj(project_file)
    file = os.path.abspath(args.file)

    file_is_in_project = file in project.source_files or file in project.include_files
    
    if not file_is_in_project:
        print(f'{file} is not in {project_file} could be:')

        could_be = difflib.get_close_matches(file, project.source_files + project.include_files)
        for could_be_file in could_be:
            print(f'{indent_for_depth(1)}{could_be_file}')
        return

    config = project.configs[0]

    if args.debug:
        print()
        print(file)
        list_include_headers(file, config.include_dirs, print_not_found, print_found, config.defines)
        print()
    else:
        max_count = args.count
        counts = collect_include_headers(file, defines=config.defines, include_dirs=config.include_dirs)

        t = counts.total()

        print(f'Total of {t} includes found')
        print(f"{max_count} most common are:")
        for include_file, count in counts.most_common(max_count):
            print(f'{indent_for_depth(1)}{include_file} ({count})')




def all_in(args):
    project_file = os.path.abspath(args.project)
    project = find_files_in_vcxproj(project_file)

    config = project.configs[0]

    counts = collections.Counter()
    number_of_files = 0

    for file in project.source_files:
        file_counts = collect_include_headers(file, defines=config.defines, include_dirs=config.include_dirs)
        counts.update(file_counts)
        number_of_files += 1

    exclude_files(counts, (os.path.abspath(f) for f in args.exclude))

    max_count = args.count
    t = counts.total()
    unique_count = len(counts)

    print(f'Total of {t} includes statements found in {number_of_files} files, {unique_count} unique includes')
    print(f"{max_count} most common are:")
    for include_file, count in counts.most_common(max_count):
        percentage = count / number_of_files * 100
        print(f'{indent_for_depth(1)}{include_file} ({count}) ({percentage:.1f}%)')





def main():
    parser = argparse.ArgumentParser(description='Tool to list headers')
    parser.set_defaults(func=lambda _: parser.print_help())

    subs = parser.add_subparsers(dest='command')

    file_parser = subs.add_parser('file', help='List headers in a single file')
    file_parser.add_argument('filename', help='File to list headers in')
    file_parser.set_defaults(func=lambda args: handle_file(args))

    file_parser = subs.add_parser('project', help='find files in a vs project file')
    file_parser.add_argument('filename', help='project file')
    file_parser.set_defaults(func=lambda args: handle_project_file(args))

    file_parser = subs.add_parser('file_in', help='list includes from a file in a vs project file')
    file_parser.add_argument('project', help='project file')
    file_parser.add_argument('file', help='file to list includes from')
    file_parser.add_argument('--debug', action='store_true', help='print debug info')
    file_parser.add_argument('--count', type=int, default=10, help='number of most common includes to print')
    file_parser.set_defaults(func=lambda args: handle_file_in_project(args))

    all_file_parser = subs.add_parser('all_in', help='list all files in a vs project file')
    all_file_parser.add_argument('project', help='project file')
    all_file_parser.add_argument('--count', type=int, default=10, help='number of most common includes to print')
    all_file_parser.add_argument('--exclude', nargs='*', help='folders to exclude', default=[])
    all_file_parser.set_defaults(func=lambda args: all_in(args))

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
