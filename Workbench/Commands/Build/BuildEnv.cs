using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Spectre.Console.Cli;
using Workbench.Shared;
using Workbench.Shared.CMake;

namespace Workbench.Commands.Build;

public enum Compiler
{
    Gcc, Clang,
    VisualStudio2015, VisualStudio2017,
    VisualStudio2019, VisualStudio2022, VisualStudio2026,
}

public enum Platform { Auto, Win32, X64 }

[TypeConverter(typeof(EnumTypeConverter<CompilersAndPlatforms>))]
[JsonConverter(typeof(EnumJsonConverter<CompilersAndPlatforms>))]
public enum CompilersAndPlatforms
{
    // compilers
    // fallbacks are github actions installed compiler

    [EnumString("gcc")]
    Gcc,

    [EnumString("clang")]
    Clang,

    [EnumString("vs2015")]
    Vs2015,
    [EnumString("vs2017", "windows-2016")]
    Vs2017,

    [EnumString("vs2019", "windows-2019")]
    Vs2019,
    [EnumString("vs2022", "windows-2022")]
    Vs2022,
    [EnumString("vs2026")]
    Vs2026,
    

    // platforms
    [EnumString("auto")]
    Auto,

    [EnumString("win32", "x86")]
    Win32,

    [EnumString("win64", "x64")]
    X64,
}

public record class CompilerName(string C, string Cpp);

public static class BuildFunctions
{
    internal static bool IsVisualStudio(Compiler c)
    {
        return c switch
        {
            Compiler.Gcc => false,
            Compiler.Clang => false,
            Compiler.VisualStudio2015 => true,
            Compiler.VisualStudio2017 => true,
            Compiler.VisualStudio2019 => true,
            Compiler.VisualStudio2022 => true,
            Compiler.VisualStudio2026 => true,
            _ => throw new ArgumentOutOfRangeException(nameof(c), c, null)
        };
    }

    internal static CompilerName? find_names(this Compiler c, bool afl)
    {
        // are theese names valid???
        return c switch
        {
            Compiler.Gcc => afl ? new CompilerName("afl-gcc", "afl-g++") : new CompilerName("gcc", "g++"),
            Compiler.Clang => afl ? new CompilerName("afl-clang", "afl-clang+") : new CompilerName("clang", "clang+"),
            Compiler.VisualStudio2015 => null,
            Compiler.VisualStudio2017 => null,
            Compiler.VisualStudio2019 => null,
            Compiler.VisualStudio2022 => null,
            Compiler.VisualStudio2026 => null,
            _ => throw new ArgumentOutOfRangeException(nameof(c), c, null)
        };
    }

    internal static string name_of_compiler(this Compiler c)
    {
        return c switch
        {
            Compiler.Gcc => "gcc",
            Compiler.Clang => "clang",
            Compiler.VisualStudio2015 => "v2015",
            Compiler.VisualStudio2017 => "vs2017",
            Compiler.VisualStudio2019 => "vs2019",
            Compiler.VisualStudio2022 => "vs2022",
            Compiler.VisualStudio2026 => "vs2026",
            _ => throw new ArgumentOutOfRangeException(nameof(c), c, null)
        };
    }
    internal static Compiler? to_compiler(this CompilersAndPlatforms c)
    {
        return c switch
        {
            CompilersAndPlatforms.Gcc => Compiler.Gcc,
            CompilersAndPlatforms.Clang => Compiler.Gcc,
            CompilersAndPlatforms.Vs2026 => Compiler.VisualStudio2026,
            CompilersAndPlatforms.Vs2022 => Compiler.VisualStudio2022,
            CompilersAndPlatforms.Vs2019 => Compiler.VisualStudio2019,
            CompilersAndPlatforms.Auto => null,
            CompilersAndPlatforms.Win32 => null,
            CompilersAndPlatforms.X64 => null,
            _ => throw new ArgumentOutOfRangeException(nameof(c), c, null)
        };
    }

    public static Platform? to_platform(this CompilersAndPlatforms c)
    {
        return c switch
        {
            CompilersAndPlatforms.Gcc => null,
            CompilersAndPlatforms.Clang => null,
            CompilersAndPlatforms.Vs2026 => null,
            CompilersAndPlatforms.Vs2022 => null,
            CompilersAndPlatforms.Vs2019 => null,
            CompilersAndPlatforms.Auto => Platform.Auto,
            CompilersAndPlatforms.Win32 => Platform.Win32,
            CompilersAndPlatforms.X64 => Platform.X64,
            _ => throw new ArgumentOutOfRangeException(nameof(c), c, null)
        };
    }

    private static bool Is64Bit(Platform platform)
    {
        return platform switch
        {
            Platform.Auto => Core.Is64Bit(),
            Platform.Win32 => false,
            Platform.X64 => true,
            _ => false,
        };
    }

    private static string GetCmakeArchitectureArgument(Platform platform)
    {
        if (Is64Bit(platform))
        {
            return "x64";
        }
        else
        {
            return "Win32";
        }
    }

    // gets the visual studio cmake generator argument for the compiler and platform
    internal static Generator CreateCmakeGenerator(Compiler compiler, Platform platform)
        => compiler switch
        {
            Compiler.VisualStudio2015 => Is64Bit(platform)
                ? new Generator("Visual Studio 14 2015 Win64")
                : new Generator("Visual Studio 14 2015"),
            Compiler.VisualStudio2017 => Is64Bit(platform)
                ? new Generator("Visual Studio 15 Win64")
                : new Generator("Visual Studio 15"),
            Compiler.VisualStudio2019 =>
                new Generator("Visual Studio 16 2019", GetCmakeArchitectureArgument(platform)),
            Compiler.VisualStudio2022 =>
                new Generator("Visual Studio 17 2022", GetCmakeArchitectureArgument(platform)),
            Compiler.Gcc => new Generator("Ninja"),
            Compiler.Clang => new Generator("Ninja"),
            Compiler.VisualStudio2026 => new Generator("Visual Studio 18 2026", GetCmakeArchitectureArgument(platform)),
            _ => throw new Exception("Invalid compiler"),
        };
}
