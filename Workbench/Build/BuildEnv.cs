using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Workbench.Utils;

namespace Workbench;

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

    [EnumString("vs2022")]
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
public class BuildEnviroment
{
    public Compiler? compiler { get; set; } = null;
    public Platform? platform { get; set; } = null;

    public static BuildEnviroment CreateEmpty()
    {
        return new BuildEnviroment();
    }

    public CMake.Generator CreateCmakeGenerator()
    {
        if (compiler == null) { throw new ArgumentNullException(nameof(compiler)); }
        if (platform == null) { throw new ArgumentNullException(nameof(platform)); }
        return BuildUitls.CreateCmakeGenerator(compiler.Value, platform.Value);
    }

    // validate the build environment
    public bool Validate(Printer printer)
    {
        var status = true;

        if (compiler == null)
        {
            printer.Error("Compiler not set");
            status = false;
        }

        if (platform == null)
        {
            printer.Error("Platform not set");
            status = false;
        }

        return status;
    }

    // update the build environment from an argparse namespace
    public void UpdateFromArguments(Printer printer, EnviromentArgument args)
    {
        UpdateCompiler(printer, args);
        UpdatePlatform(printer, args);

        void UpdateCompiler(Printer printer, EnviromentArgument args)
        {
            if (args.Compiler == null) { return; }

            if (compiler == null)
            {
                compiler = args.Compiler;
                return;
            }

            if (args.Compiler == compiler) { return; }

            if (args.ForceChange)
            {
                printer.Warning($"Compiler changed via argument from {compiler} to {args.Compiler}");
                compiler = args.Compiler;
            }
            else
            {
                printer.Error($"Compiler changed via argument from {compiler} to {args.Compiler}");
            }
        }

        void UpdatePlatform(Printer printer, EnviromentArgument args)
        {
            if (args.Platform == null) { return; }

            if (platform == null)
            {
                platform = args.Platform;
                return;
            }

            if (args.Platform == platform) { return; }

            if (args.ForceChange)
            {
                printer.Warning($"Platform changed via argument from {platform} to {args.Platform}");
                platform = args.Platform;
            }
            else
            {
                printer.Error($"Platform changed via argument from {platform} to {args.Platform}");
            }
        }
    }
}


public class EnviromentArgument : CommandSettings
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

public static class BuildUitls
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

    private static string GetCmakeArchitctureArgument(Platform platform)
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
    {
        return compiler switch
        {
            Compiler.VisualStudio2015 => Is64Bit(platform)
                ? new CMake.Generator("Visual Studio 14 2015 Win64")
                : new CMake.Generator("Visual Studio 14 2015"),
            Compiler.VisualStudio2017 => Is64Bit(platform)
                ? new CMake.Generator("Visual Studio 15 Win64")
                : new CMake.Generator("Visual Studio 15"),
            Compiler.VisualStudio2019 =>
                new CMake.Generator("Visual Studio 16 2019", GetCmakeArchitctureArgument(platform)),
            Compiler.VisualStudio2022 =>
                new CMake.Generator("Visual Studio 17 2022", GetCmakeArchitctureArgument(platform)),
            _ => throw new Exception("Invalid compiler"),
        };
    }

    public static void SaveToFile(BuildEnviroment self, string path)
    {
        File.WriteAllText(path, JsonUtil.Write(self));
    }

    // load build enviroment from json file
    public static BuildEnviroment LoadFromFileOrCreateEmpty(string path, Printer printer)
    {
        if (File.Exists(path) == false)
        {
            return BuildEnviroment.CreateEmpty();
        }

        var content = File.ReadAllText(path);

        var loaded = JsonUtil.Parse<BuildEnviroment>(printer, path, content);
        if (loaded == null)
        {
            return BuildEnviroment.CreateEmpty();
        }

        return loaded;
    }
}
