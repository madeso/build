#!/usr/bin/python3
"""provides compiler and plaform enum and argparse related functions"""

import typing
from enum import Enum

import buildtools.core as core


class Compiler(Enum):
    """list of compilers"""
    VS2015 = 1
    VS2017 = 2
    VS2019 = 3


class Platform(Enum):
    """list of platforms"""
    AUTO = 0
    WIN32 = 1
    X64 = 2


COMPILER_NAME_VS2015 = 'vs2015'
COMPILER_NAME_VS2017 = 'vs2017'
COMPILER_NAME_VS2019 = 'vs2019'
COMPILER_NAME_WINDOWS_2016 = 'windows-2016'
COMPILER_NAME_WINDOWS_2019 = 'windows-2019'


def compiler_from_name(compiler_name: str, print_error: bool) -> typing.Optional[Compiler]:
    if compiler_name.lower() == COMPILER_NAME_VS2015:
        return Compiler.VS2015

    elif compiler_name.lower() == COMPILER_NAME_VS2017:
        return Compiler.VS2017

    elif compiler_name.lower() == COMPILER_NAME_VS2019:
        return Compiler.VS2019

    # github actions installed compiler
    elif compiler_name.lower() == COMPILER_NAME_WINDOWS_2016:
        return Compiler.VS2017
    elif compiler_name.lower() == COMPILER_NAME_WINDOWS_2019:
        return Compiler.VS2019

    if print_error:
        print('Unknown compiler: ', compiler_name, flush=True)
    return None


def all_compiler_names():
    """returns a list of all compiler names"""
    return [COMPILER_NAME_VS2015, COMPILER_NAME_VS2017, COMPILER_NAME_VS2019, COMPILER_NAME_WINDOWS_2016, COMPILER_NAME_WINDOWS_2019]


def compiler_to_string(compiler: Compiler) -> str:
    if compiler == Compiler.VS2015:
        return COMPILER_NAME_VS2015
    elif compiler == Compiler.VS2017:
        return COMPILER_NAME_VS2017
    elif compiler == Compiler.VS2019:
        return COMPILER_NAME_VS2019
    else:
        return "<unknown compiler>"

PLATFORM_AUTO = 'auto'
PLATFORM_WIN32 = 'win32'
PLATFORM_WIN64 = 'win64'
PLATFORM_WIN64_ALT = 'x64'

def platform_from_name(platform: str, print_error: bool) -> typing.Optional[Platform]:
    if platform.lower() == PLATFORM_AUTO:
        return Platform.AUTO

    if platform.lower() == PLATFORM_WIN32:
        return Platform.WIN32

    if platform.lower() in [PLATFORM_WIN64, PLATFORM_WIN64_ALT]:
        return Platform.X64

    if print_error:
        print('Unknown platform: ', platform, flush=True)
    return None


def all_platform_names():
    """returns a list of all platform names"""
    return [PLATFORM_AUTO, PLATFORM_WIN32, PLATFORM_WIN64, PLATFORM_WIN64_ALT]

def platform_to_string(platform: Platform) -> str:
    if platform == Platform.AUTO:
        return PLATFORM_AUTO
    elif platform == Platform.WIN32:
        return PLATFORM_WIN32
    elif platform == Platform.X64:
        return PLATFORM_WIN64
    else:
        return "<unknown platform>"

#####################################################


def get_msbuild_toolset(compiler: Compiler) -> typing.Optional[str]:
    """get the msbuild tooselt name from the compiler"""
    if compiler == Compiler.VS2015:
        return 'v140'

    if compiler == Compiler.VS2017:
        return 'v141'

    if compiler == Compiler.VS2019:
        return 'v142'

    return None


def is_64bit(platform: Platform) -> bool:
    """get if the platform is 64 bit or not"""
    if platform == Platform.WIN32:
        return False

    if platform == Platform.X64:
        return True

    if core.is_platform_64bit():
        return True

    return False


def platform_as_string(platform: Platform) -> str:
    """returns either a 64 or a 32 bit string identification"""
    if is_64bit(platform):
        return 'x64'

    return 'win32'
