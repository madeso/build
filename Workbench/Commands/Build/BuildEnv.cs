using System.ComponentModel;
using System.Text.Json.Serialization;
using Spectre.Console.Cli;
using Workbench.Shared;
using Workbench.Shared.CMake;

namespace Workbench.Commands.Build;

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

    public Generator CreateCmakeGenerator()
    {
        if (Compiler == null) { throw new ArgumentNullException(nameof(Compiler)); }
        if (Platform == null) { throw new ArgumentNullException(nameof(Platform)); }
        return BuildFunctions.CreateCmakeGenerator(Compiler.Value, Platform.Value);
    }

    // validate the build environment
    public bool Validate(Log log)
    {
        var status = true;

        if (Compiler == null)
        {
            log.Error("Compiler not set");
            status = false;
        }

        if (Platform == null)
        {
            log.Error("Platform not set");
            status = false;
        }

        return status;
    }

    // update the build environment from an argparse namespace
    public void UpdateFromArguments(Log log, EnvironmentArgument args)
    {
        update_compiler();
        update_platform();
        return;

        void update_compiler()
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
                log.Warning($"Compiler changed via argument from {Compiler} to {args.Compiler}");
                Compiler = args.Compiler;
            }
            else
            {
                log.Error($"Compiler changed via argument from {Compiler} to {args.Compiler}");
            }
        }

        void update_platform()
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
                log.Warning($"Platform changed via argument from {Platform} to {args.Platform}");
                Platform = args.Platform;
            }
            else
            {
                log.Error($"Platform changed via argument from {Platform} to {args.Platform}");
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
            _ => throw new Exception("Invalid compiler"),
        };

    public static void SaveToFile(Vfs vfs, BuildEnvironment self, Fil path)
    {
        path.WriteAllText(vfs, JsonUtil.Write(self));
    }

    // load build environment from json file
    public static BuildEnvironment LoadFromFileOrCreateEmpty(Vfs vfs, Fil path, Log log)
    {
        if (path.Exists(vfs) == false)
        {
            return BuildEnvironment.CreateEmpty();
        }

        var content = path.ReadAllText(vfs);

        var loaded = JsonUtil.Parse<BuildEnvironment>(log, path, content);
        if (loaded == null)
        {
            return BuildEnvironment.CreateEmpty();
        }

        return loaded;
    }
}
