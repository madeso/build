using System.Text.RegularExpressions;
using Workbench.CMake;

namespace Workbench;

// list of compilers
// #[derive(Clone, Serialize, Deserialize, Debug, PartialEq)]
public enum Compiler
{
    VisualStudio2015,
    VisualStudio2017,
    VisualStudio2019,
    VisualStudio2022
}


// list of platforms
// #[derive(Clone, Serialize, Deserialize, Debug, PartialEq)]
public enum Platform
{
    Auto,
    Win32,
    X64
}

// #[derive(Serialize, Deserialize, Debug)]
public class BuildEnviroment
{
    public Compiler? compiler = null;
    public Platform? platform = null;

    public static BuildEnviroment new_empty()
    {
        return new BuildEnviroment();
    }

    public CMake.Generator get_cmake_generator()
    {
        if (compiler == null) { throw new ArgumentNullException(nameof(compiler)); }
        if (platform == null) { throw new ArgumentNullException(nameof(platform)); }
        return BuildUitls.create_cmake_generator(compiler.Value, platform.Value);
    }

    // validate the build environment
    public bool validate(Printer printer)
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
    public void update_from_args(Printer printer, EnviromentArgument args)
    {
        updateCompiler(printer, args);
        updatePlatform(printer, args);

        void updateCompiler(Printer printer, EnviromentArgument args)
        {
            if (args.compiler == null) { return; }

            if (this.compiler == null)
            {
                this.compiler = args.compiler;
                return;
            }

            if (args.compiler == this.compiler) { return; }

            if (args.force)
            {
                printer.warning($"Compiler changed via argument from {this.compiler} to {args.compiler}");
                this.compiler = args.compiler;
            }
            else
            {
                printer.error($"Compiler changed via argument from {this.compiler} to {args.compiler}");
            }
        }

        void updatePlatform(Printer printer, EnviromentArgument args)
        {
            if (args.platform == null) { return; }

            if (this.platform == null)
            {
                this.platform = args.platform;
                return;
            }

            if (args.platform == this.platform) { return; }

            if (args.force)
            {
                printer.warning($"Platform changed via argument from {this.platform} to {args.platform}");
                this.platform = args.platform;
            }
            else
            {
                printer.error($"Platform changed via argument from {this.platform} to {args.platform}");
            }
        }
    }
}


public class EnviromentArgument
{
    // #[structopt(long)]
    public Compiler? compiler { get; set; }

    // #[structopt(long)]
    public Platform? platform { get; set; }

    // #[structopt(long)]
    public bool force { get; set; }
}

public static class BuildUitls
{
    static Compiler? compiler_from_string(string input)
    {
        return input.ToLowerInvariant() switch
        {
            "vs2015" => Compiler.VisualStudio2015,
            "vs2017" => Compiler.VisualStudio2017,
            "vs2019" => Compiler.VisualStudio2019,
            "vs2022" => Compiler.VisualStudio2022,
            // github actions installed compiler
            "windows-2016" => Compiler.VisualStudio2017,
            "windows-2019" => Compiler.VisualStudio2019,
            _ => null,
        };
    }

    static Platform? platform_from_string(string input)
    {
        return input.ToLowerInvariant() switch
        {
            "auto" => Platform.Auto,
            "win32" => Platform.Win32,
            "x64" => Platform.X64,
            "win64" => Platform.X64,
            _ => null,
        };
    }

    static bool is_64bit(Platform platform)
    {
        return platform switch
        {
            Platform.Auto => Core.is_64bit(),
            Platform.Win32 => false,
            Platform.X64 => true,
            _ => false,
        };
    }


    static string create_cmake_arch(Platform platform)
    {
        if (is_64bit(platform))
        {
            return "x64";
        }
        else
        {
            return "Win32";
        }
    }

    // gets the visual studio cmake generator argument for the compiler and platform
    internal static CMake.Generator create_cmake_generator(Compiler compiler, Platform platform)
    {
        switch (compiler)
        {
            case Compiler.VisualStudio2015:
                return is_64bit(platform)
                    ? new CMake.Generator("Visual Studio 14 2015 Win64")
                    : new CMake.Generator("Visual Studio 14 2015")
                    ;
            case Compiler.VisualStudio2017:
                return is_64bit(platform)
                    ? new CMake.Generator("Visual Studio 15 Win64")
                    : new CMake.Generator("Visual Studio 15")
                    ;
            case Compiler.VisualStudio2019:
                return new CMake.Generator("Visual Studio 16 2019", create_cmake_arch(platform));
            case Compiler.VisualStudio2022:
                return new CMake.Generator("Visual Studio 17 2022", create_cmake_arch(platform));
            default:
                throw new Exception("Invalid compiler");
        }
    }

    public static void save_to_file(BuildEnviroment self, string path)
    {
        File.WriteAllText(path, JsonUtil.Write(self));
    }

    // load build enviroment from json file
    public static BuildEnviroment load_from_file(string path, Printer printer)
    {
        if (File.Exists(path) == false)
        {
            return BuildEnviroment.new_empty();
        }

        var content = File.ReadAllText(path);

        var loaded = JsonUtil.Parse<BuildEnviroment>(printer, path, content);
        if (loaded == null)
        {
            return BuildEnviroment.new_empty();
        }

        return loaded;
    }
}
