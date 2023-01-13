using Spectre.Console.Cli;
using System.ComponentModel;

namespace Workbench;

[TypeConverter(typeof(CompilerConverter))]
public enum Compiler
{
    VisualStudio2015,
    VisualStudio2017,
    VisualStudio2019,
    VisualStudio2022
}

class CompilerConverter : EnumTypeConverter<Compiler>
{
    public CompilerConverter()
    {
        // fallbacks: github actions installed compiler
        Data
            .Add(Compiler.VisualStudio2015, "vs2015")
            .Add(Compiler.VisualStudio2017, "vs2017", "windows-2016")
            .Add(Compiler.VisualStudio2019, "vs2019", "windows-2019")
            .Add(Compiler.VisualStudio2022, "vs2022")
            ;
    }
}


[TypeConverter(typeof(PlatformConverter))]
public enum Platform
{
    Auto,
    Win32,
    X64
}

class PlatformConverter : EnumTypeConverter<Platform>
{
    public PlatformConverter()
    {
        Data
            .Add(Platform.Auto, "auto")
            .Add(Platform.Win32, "win32", "x86")
            .Add(Platform.X64, "win64", "x64")
            ;
    }
}

// #[derive(Serialize, Deserialize, Debug)]
public class BuildEnviroment
{
    public Compiler? compiler = null;
    public Platform? platform = null;

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
            printer.error("Compiler not set");
            status = false;
        }

        if (platform == null)
        {
            printer.error("Platform not set");
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

            if (this.compiler == null)
            {
                this.compiler = args.Compiler;
                return;
            }

            if (args.Compiler == this.compiler) { return; }

            if (args.ForceChange)
            {
                printer.warning($"Compiler changed via argument from {this.compiler} to {args.Compiler}");
                this.compiler = args.Compiler;
            }
            else
            {
                printer.error($"Compiler changed via argument from {this.compiler} to {args.Compiler}");
            }
        }

        void UpdatePlatform(Printer printer, EnviromentArgument args)
        {
            if (args.Platform == null) { return; }

            if (this.platform == null)
            {
                this.platform = args.Platform;
                return;
            }

            if (args.Platform == this.platform) { return; }

            if (args.ForceChange)
            {
                printer.warning($"Platform changed via argument from {this.platform} to {args.Platform}");
                this.platform = args.Platform;
            }
            else
            {
                printer.error($"Platform changed via argument from {this.platform} to {args.Platform}");
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
    static bool Is64Bit(Platform platform)
    {
        return platform switch
        {
            Platform.Auto => Core.Is64Bit(),
            Platform.Win32 => false,
            Platform.X64 => true,
            _ => false,
        };
    }


    static string GetCmakeArchitctureArgument(Platform platform)
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
        switch (compiler)
        {
            case Compiler.VisualStudio2015:
                return Is64Bit(platform)
                    ? new CMake.Generator("Visual Studio 14 2015 Win64")
                    : new CMake.Generator("Visual Studio 14 2015")
                    ;
            case Compiler.VisualStudio2017:
                return Is64Bit(platform)
                    ? new CMake.Generator("Visual Studio 15 Win64")
                    : new CMake.Generator("Visual Studio 15")
                    ;
            case Compiler.VisualStudio2019:
                return new CMake.Generator("Visual Studio 16 2019", GetCmakeArchitctureArgument(platform));
            case Compiler.VisualStudio2022:
                return new CMake.Generator("Visual Studio 17 2022", GetCmakeArchitctureArgument(platform));
            default:
                throw new Exception("Invalid compiler");
        }
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
