using System.ComponentModel;
using System.Text.Json.Serialization;
using Spectre.Console;
using Workbench.Shared;
using Workbench.Shared.CMake;

namespace Workbench.Commands.Build;

[TypeConverter(typeof(EnumTypeConverter<DependencyName>))]
[JsonConverter(typeof(EnumJsonConverter<DependencyName>))]
public enum DependencyName
{
    [EnumString("sdl-2.0.8")]
    Sdl2dot0dot8,

    [EnumString("sdl-2.0.20")]
    Sdl2dot0dot20,

    [EnumString("python")]
    Python,

    [EnumString("assimp")]
    Assimp,

    [EnumString("assimp_static")]
    AssimpStatic,

    [EnumString("wxWidgets")]
    WxWidgets,

    // earlier dependencies were wxWidgets and to a lesser extent: boost and libxml
    // if we need them we should probably replace this whole setup with a package manager
}

public interface BuildDependency
{
    string GetName();

    // add arguments to the main cmake
    void AddCmakeArguments(CMakeProject cmake);

    // install the dependency
    public void Install(BuildEnvironment env, Printer print, BuildData data);

    // get the status of the dependency
    public IEnumerable<string> GetStatus();
}

public static class BuildDependencies
{
    public static BuildDependency CreateDependency(DependencyName name, BuildData data)
    {
        return name switch
        {
            DependencyName.Sdl2dot0dot8 => new DependencySdl2(data, "https://www.libsdl.org/release/SDL2-2.0.8.zip", "SDL2-2.0.8"),
            DependencyName.Sdl2dot0dot20 => new DependencySdl2(data, "https://www.libsdl.org/release/SDL2-2.0.20.zip", "SDL2-2.0.20"),
            DependencyName.Python => new DependencyPython(),
            DependencyName.Assimp => new DependencyAssimp(data, false),
            DependencyName.AssimpStatic => new DependencyAssimp(data, true),
            DependencyName.WxWidgets => new DependencyWxWidgets(data, "https://github.com/wxWidgets/wxWidgets/releases/download/v3.2.2.1/wxWidgets-3.2.2.1.zip", "wxWidgets-3-2-2-1"),
            _ => throw new Exception($"invalid name: {name}"),
        };
    }
}

internal class DependencySdl2 : BuildDependency
{
    private readonly string root_folder;
    private readonly string build_folder;
    private readonly string url;
    private readonly string folder_name;

    public DependencySdl2(BuildData data, string url_to_zip_file, string folder_name)
    {
        root_folder = Path.Combine(data.DependencyDirectory, folder_name);
        build_folder = Path.Combine(root_folder, "cmake-build");
        url = url_to_zip_file;
        this.folder_name = folder_name;
    }

    public string GetName()
    {
        return "sdl2";
    }

    public void AddCmakeArguments(CMakeProject cmake)
    {
        cmake.AddArgument("SDL2_HINT_ROOT", root_folder);
        cmake.AddArgument("SDL2_HINT_BUILD", build_folder);
    }

    public void Install(BuildEnvironment env, Printer print, BuildData data)
    {
        var generator = env.CreateCmakeGenerator();

        Printer.Header("Installing dependency sdl2");

        var zip_file = Path.Join(data.DependencyDirectory, $"{folder_name}.zip");

        if (false == File.Exists(zip_file))
        {
            Core.VerifyDirectoryExists(print, root_folder);
            Core.VerifyDirectoryExists(print, data.DependencyDirectory);
            AnsiConsole.WriteLine("downloading sdl2");
            Core.DownloadFileIfMissing(print, url, zip_file);
        }
        else
        {
            AnsiConsole.WriteLine("SDL2 zip file exist, not downloading again...");
        }

        if (false == File.Exists(Path.Join(root_folder, "INSTALL.txt")))
        {
            Core.ExtractZip(zip_file, root_folder);
            Core.MoveFiles(print, Path.Join(root_folder, folder_name), root_folder);
        }
        else
        {
            AnsiConsole.WriteLine("SDL2 is unzipped, not unzipping again");
        }

        if (false == File.Exists(Path.Join(build_folder, "SDL2.sln")))
        {
            var project = new CMakeProject(build_folder, root_folder, generator);
            // project.make_static_library()
            // this is defined by the standard library so don't add it
            // generates '__ftol2_sse already defined' errors
            project.AddArgument("LIBC", "ON");
            project.AddArgument("SDL_STATIC", "ON");
            project.AddArgument("SDL_SHARED", "OFF");
            project.Configure(print);
            project.Build(print, Shared.CMake.Config.Release);
        }
        else
        {
            AnsiConsole.WriteLine("SDL2 build exist, not building again...");
        }
    }

    public IEnumerable<string> GetStatus()
    {
        yield return $"Root: {root_folder}";
        yield return $"Build: {build_folder}";
    }
}


///////////////////////////////////////////////////////////////////////////////////////////////////

internal class DependencyPython : BuildDependency
{
    private readonly string? path_to_python_exe;

    internal DependencyPython()
    {
        path_to_python_exe = Environment.GetEnvironmentVariable("PYTHON");
    }

    public string GetName()
    {
        return "python";
    }

    public void AddCmakeArguments(CMakeProject cmake)
    {
        if (path_to_python_exe == null) { return; }

        var python_exe = Path.Join(path_to_python_exe, "python.exe");
        cmake.AddArgument("PYTHON_EXECUTABLE:FILEPATH", python_exe);
    }

    public void Install(BuildEnvironment env, Printer print, BuildData data)
    {
    }

    public IEnumerable<string> GetStatus()
    {
        if (path_to_python_exe != null)
        {
            yield return $"PYTHON: {path_to_python_exe}";
        }
        else
        {
            yield return "Couldn't interpret PYTHON";
        }
    }
}


internal class DependencyAssimp : BuildDependency
{
    private readonly string dependency_folder;
    private readonly string install_folder;
    private readonly bool use_static_build;

    public DependencyAssimp(BuildData data, bool use_static_build)
    {
        dependency_folder = Path.Join(data.DependencyDirectory, "assimp");
        install_folder = Path.Join(dependency_folder, "cmake-install");
        this.use_static_build = use_static_build;
    }

    public string GetName()
    {
        return "assimp";
    }

    public void AddCmakeArguments(CMakeProject cmake)
    {
        cmake.AddArgument("ASSIMP_ROOT_DIR", install_folder);
    }

    public void Install(BuildEnvironment env, Printer print, BuildData data)
    {
        const string URL = "https://github.com/assimp/assimp/archive/v5.0.1.zip";

        var generator = env.CreateCmakeGenerator();

        Printer.Header("Installing dependency assimp");
        var zip_file = Path.Join(data.DependencyDirectory, "assimp.zip");
        if (false == Directory.Exists(dependency_folder))
        {
            Core.VerifyDirectoryExists(print, dependency_folder);
            Core.VerifyDirectoryExists(print, data.DependencyDirectory);
            AnsiConsole.WriteLine("downloading assimp");
            Core.DownloadFileIfMissing(print, URL, zip_file);
            AnsiConsole.WriteLine("extracting assimp");
            Core.ExtractZip(zip_file, dependency_folder);
            var build = Path.Join(dependency_folder, "cmake-build");
            Core.MoveFiles(print, Path.Join(dependency_folder, "assimp-5.0.1"), dependency_folder);

            var project = new CMakeProject(build, dependency_folder, generator);
            project.AddArgument("ASSIMP_BUILD_X3D_IMPORTER", "0");
            if (use_static_build)
            {
                project.MakeStaticLibrary();
            }
            AnsiConsole.WriteLine($"Installing cmake to {install_folder}");
            project.SetInstallFolder(install_folder);
            Core.VerifyDirectoryExists(print, install_folder);

            project.Configure(print);
            project.Build(print, Shared.CMake.Config.Release);

            AnsiConsole.WriteLine("Installing assimp");
            project.Install(print, Shared.CMake.Config.Release);
        }
        else
        {
            AnsiConsole.WriteLine("Assimp build exist, not building again...");
        }
    }

    public IEnumerable<string> GetStatus()
    {
        yield return $"Root: {dependency_folder}";
        yield return $"Install: {install_folder}";
    }
}

internal class DependencyWxWidgets : BuildDependency
{
    private readonly string root_folder;
    private readonly string build_folder;
    private readonly string url;
    private readonly string folder_name;

    public DependencyWxWidgets(BuildData data, string url_to_zip_file, string folder_name)
    {
        root_folder = Path.Combine(data.DependencyDirectory, folder_name);
        build_folder = Path.Combine(root_folder, "cmake-build");
        url = url_to_zip_file;
        this.folder_name = folder_name;
    }

    public string GetName()
    {
        return "wxWidgets";
    }

    public void AddCmakeArguments(CMakeProject cmake)
    {
        // if these differs it clears the lib dir... but also one is required to use / on windows... wtf!
        cmake.AddArgument("WX_ROOT_DIR", root_folder.Replace('\\', '/'));
        cmake.AddArgument("wxWidgets_ROOT_DIR", root_folder);
        cmake.AddArgument("wxWidgets_CONFIGURATION", "mswu");

        cmake.AddArgument("wxWidgets_USE_REL_AND_DBG", "ON"); // require both debug and release

        // perhaps replace \ with /
        string p = GetLibraryFolder();
        p = p.Replace('\\', '/');
        if (p.EndsWith('/') == false) { p += '/'; }
        cmake.AddArgument("wxWidgets_LIB_DIR", p);
    }

    // todo(Gustav): switch this when building 32 bit
    private string GetLibraryFolder()
        => Path.Join(build_folder, "lib", "vc_x64_lib");

    public void Install(BuildEnvironment env, Printer print, BuildData data)
    {
        var generator = env.CreateCmakeGenerator();

        Printer.Header("Installing dependency wxwidgets");

        var zip_file = Path.Join(data.DependencyDirectory, $"{folder_name}.zip");

        if (false == File.Exists(zip_file))
        {
            Core.VerifyDirectoryExists(print, root_folder);
            Core.VerifyDirectoryExists(print, data.DependencyDirectory);
            AnsiConsole.WriteLine("downloading wxwidgets");
            Core.DownloadFileIfMissing(print, url, zip_file);
        }
        else
        {
            AnsiConsole.WriteLine("wxWidgets zip file exist, not downloading again...");
        }

        if (false == File.Exists(Path.Join(root_folder, "CMakeLists.txt")))
        {
            Core.ExtractZip(zip_file, root_folder);
        }
        else
        {
            AnsiConsole.WriteLine("wxWidgets is unzipped, not unzipping again");
        }

        var build_dbg = false == File.Exists(Path.Join(GetLibraryFolder(), "wxzlibd.lib"));
        var build_rel = false == File.Exists(Path.Join(GetLibraryFolder(), "wxzlib.lib"));

        if (build_dbg || build_rel)
        {
            var project = ConfigProject(print, root_folder, build_folder, generator);
            if (build_dbg)
            {
                AnsiConsole.WriteLine("building debug wxWidgets");
                project.Build(print, Shared.CMake.Config.Debug);
            }

            if (build_rel)
            {
                AnsiConsole.WriteLine("building release wxWidgets");
                project.Build(print, Shared.CMake.Config.Release);
            }
        }
        else
        {
            AnsiConsole.WriteLine("wxWidgets build exist, not building again...");
        }
    }

    private static CMakeProject ConfigProject(Printer print, string root, string build, Generator generator)
    {
        var project = new CMakeProject(build, root, generator);
        project.AddArgument("LIBC", "ON");
        project.AddArgument("wxBUILD_SHARED", "OFF");
        project.Configure(print);
        return project;
    }

    public IEnumerable<string> GetStatus()
    {
        yield return $"Root: {root_folder}";
        yield return $"Build: {build_folder}";
    }
}
