using System.ComponentModel;
using System.Text.Json.Serialization;
using Spectre.Console.Cli;
using Workbench.Utils;

namespace Workbench.Build;

[TypeConverter(typeof(EnumTypeConverter<Compiler>))]
[JsonConverter(typeof(EnumJsonConverter<Compiler>))]
public enum Compiler
{
    // fallbacks are github actions installed compiler

    [EnumString("vs2015")]
    VisualStudio2015,

    [EnumString("vs2017", "windows-2016")]
    VisualStudio2017,

    [EnumString("vs2019", "windows-2019")]
    VisualStudio2019,

    [EnumString("vs2022", "windows-2022")]
    VisualStudio2022,
}


[TypeConverter(typeof(EnumTypeConverter<Platform>))]
[JsonConverter(typeof(EnumJsonConverter<Platform>))]
public enum Platform
{
    [EnumString("auto")]
    Auto,

    [EnumString("win32", "x86")]
    Win32,

    [EnumString("win64", "x64")]
    X64,
}


// #[derive(Serialize, Deserialize, Debug)]
public class BuildEnvironment
{
    public Compiler? Compiler { get; set; } = null;
    public Platform? Platform { get; set; } = null;

    public static BuildEnvironment CreateEmpty()
    {
        return new BuildEnvironment();
    }

    public CMake.Generator CreateCmakeGenerator()
    {
        if (Compiler == null) { throw new ArgumentNullException(nameof(Compiler)); }
        if (Platform == null) { throw new ArgumentNullException(nameof(Platform)); }
        return BuildFunctions.CreateCmakeGenerator(Compiler.Value, Platform.Value);
    }

    internal string CreateMsBuildPlatform()
    {
        if (Compiler == null) { throw new ArgumentNullException(nameof(Compiler)); }
        if (Platform == null) { throw new ArgumentNullException(nameof(Platform)); }
        return BuildFunctions.CreateMsBuildPlatform(Compiler.Value, Platform.Value);
    }

    // validate the build environment
    public bool Validate(Printer printer)
    {
        var status = true;

        if (Compiler == null)
        {
            printer.Error("Compiler not set");
            status = false;
        }

        if (Platform == null)
        {
            printer.Error("Platform not set");
            status = false;
        }

        return status;
    }

    // update the build environment from an argparse namespace
    public void UpdateFromArguments(Printer printer, EnvironmentArgument args)
    {
        UpdateCompiler();
        UpdatePlatform();
        return;

        void UpdateCompiler()
        {
            if (args.Compiler == null) { return; }

            if (Compiler == null)
            {
                Compiler = args.Compiler;
                return;
            }

            if (args.Compiler == Compiler) { return; }

            if (args.ForceChange)
            {
                printer.Warning($"Compiler changed via argument from {Compiler} to {args.Compiler}");
                Compiler = args.Compiler;
            }
            else
            {
                printer.Error($"Compiler changed via argument from {Compiler} to {args.Compiler}");
            }
        }

        void UpdatePlatform()
        {
            if (args.Platform == null) { return; }

            if (Platform == null)
            {
                Platform = args.Platform;
                return;
            }

            if (args.Platform == Platform) { return; }

            if (args.ForceChange)
            {
                printer.Warning($"Platform changed via argument from {Platform} to {args.Platform}");
                Platform = args.Platform;
            }
            else
            {
                printer.Error($"Platform changed via argument from {Platform} to {args.Platform}");
            }
        }
    }
}


public class EnvironmentArgument : CommandSettings
{
    [Description("The compiler to use")]
    [CommandOption("--compiler")]
    [DefaultValue(null)]
    public Compiler? Compiler { get; set; }

    [Description("The platform to use")]
    [CommandOption("--platform")]
    [DefaultValue(null)]
    public Platform? Platform { get; set; }

    [Description("force a change if the compiler or platform differs from last time")]
    [CommandOption("--force")]
    [DefaultValue(false)]
    public bool ForceChange { get; set; }
}

public static class BuildFunctions
{
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
    internal static CMake.Generator CreateCmakeGenerator(Compiler compiler, Platform platform)
        => compiler switch
        {
            Compiler.VisualStudio2015 => Is64Bit(platform)
                ? new CMake.Generator("Visual Studio 14 2015 Win64")
                : new CMake.Generator("Visual Studio 14 2015"),
            Compiler.VisualStudio2017 => Is64Bit(platform)
                ? new CMake.Generator("Visual Studio 15 Win64")
                : new CMake.Generator("Visual Studio 15"),
            Compiler.VisualStudio2019 =>
                new CMake.Generator("Visual Studio 16 2019", GetCmakeArchitectureArgument(platform)),
            Compiler.VisualStudio2022 =>
                new CMake.Generator("Visual Studio 17 2022", GetCmakeArchitectureArgument(platform)),
            _ => throw new Exception("Invalid compiler"),
        };

    internal static string CreateMsBuildPlatform(Compiler compiler, Platform platform)
        => platform switch
        {
            Platform.Win32 => "Win32",
            Platform.X64 => "\x64",
            Platform.Auto => throw new Exception("Invalid setting..."),
            _ => throw new Exception("Invalid compiler"),
        };

    public static void SaveToFile(BuildEnvironment self, string path)
    {
        File.WriteAllText(path, JsonUtil.Write(self));
    }

    // load build environment from json file
    public static BuildEnvironment LoadFromFileOrCreateEmpty(string path, Printer printer)
    {
        if (File.Exists(path) == false)
        {
            return BuildEnvironment.CreateEmpty();
        }

        var content = File.ReadAllText(path);

        var loaded = JsonUtil.Parse<BuildEnvironment>(printer, path, content);
        if (loaded == null)
        {
            return BuildEnvironment.CreateEmpty();
        }

        return loaded;
    }
}
