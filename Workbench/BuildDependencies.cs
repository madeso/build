using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Workbench;

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
    AssimpStatic

    // earlier dependencies were wxWidgets and to a lesser extent: boost and libxml
    // if we need them we should probably replace this whole setup with a package manager
}

public interface Dependency
{
    string GetName();

    // add arguments to the main cmake
    void AddCmakeArguments(CMake.CMake cmake);

    // install the dependency
    public void Install(BuildEnviroment env, Printer print, BuildData data);

    // get the status of the dependency
    public IEnumerable<string> GetStatus();
}

public static class Dependencies
{
    public static Dependency CreateDependency(DependencyName name, BuildData data)
    {
        return name switch
        {
            DependencyName.Sdl2dot0dot8 => new DependencySdl2(data, "https://www.libsdl.org/release/SDL2-2.0.8.zip", "SDL2-2.0.8"),
            DependencyName.Sdl2dot0dot20 => new DependencySdl2(data, "https://www.libsdl.org/release/SDL2-2.0.20.zip", "SDL2-2.0.20"),
            DependencyName.Python => new DependencyPython(),
            DependencyName.Assimp => new DependencyAssimp(data, false),
            DependencyName.AssimpStatic => new DependencyAssimp(data, true),
            _ => throw new Exception($"invalid name: {name}"),
        };
    }
}

internal class DependencySdl2 : Dependency
{
    private readonly string root_folder;
    private readonly string build_folder;
    private readonly string url;
    private readonly string folderName;

    public DependencySdl2(BuildData data, string zipFile, string folderName)
    {
        root_folder = Path.Combine(data.DependencyDirectory, folderName);
        build_folder = Path.Combine(root_folder, "cmake-build");
        url = zipFile;
        this.folderName = folderName;
    }

    public string GetName()
    {
        return "sdl2";
    }

    public void AddCmakeArguments(CMake.CMake cmake)
    {
        cmake.AddArgument("SDL2_HINT_ROOT", root_folder);
        cmake.AddArgument("SDL2_HINT_BUILD", build_folder);
    }

    public void Install(BuildEnviroment env, Printer print, BuildData data)
    {
        var deps = data.DependencyDirectory;
        var root = root_folder;
        var build = build_folder;
        var generator = env.CreateCmakeGenerator();

        print.Header("Installing dependency sdl2");

        var zip_file = Path.Join(deps, $"{folderName}.zip");

        if (false == File.Exists(zip_file))
        {
            Core.VerifyDirectoryExists(print, root);
            Core.VerifyDirectoryExists(print, deps);
            print.Info("downloading sdl2");
            Core.DownloadFileIfMissing(print, url, zip_file);
        }
        else
        {
            print.Info("SDL2 zip file exist, not downloading again...");
        }

        if (false == File.Exists(Path.Join(root, "INSTALL.txt")))
        {
            Core.ExtractZip(print, zip_file, root);
            Core.MoveFiles(print, Path.Join(root, folderName), root);
        }
        else
        {
            print.Info("SDL2 is unzipped, not unzipping again");
        }

        if (false == File.Exists(Path.Join(build, "SDL2.sln")))
        {
            var project = new CMake.CMake(build, root, generator);
            // project.make_static_library()
            // this is defined by the standard library so don't add it
            // generates '__ftol2_sse already defined' errors
            project.AddArgument("LIBC", "ON");
            project.AddArgument("SDL_STATIC", "ON");
            project.AddArgument("SDL_SHARED", "OFF");
            project.Configure(print);
            project.Build(print);
        }
        else
        {
            print.Info("SDL2 build exist, not building again...");
        }
    }

    public IEnumerable<string> GetStatus()
    {
        yield return $"Root: {root_folder}";
        yield return $"Build: {build_folder}";
    }
}

///////////////////////////////////////////////////////////////////////////////////////////////////

internal class DependencyPython : Dependency
{
    private readonly string? pathToPythonExe;

    internal DependencyPython()
    {
        pathToPythonExe = Environment.GetEnvironmentVariable("PYTHON");
    }

    public string GetName()
    {
        return "python";
    }

    public void AddCmakeArguments(CMake.CMake cmake)
    {
        if (pathToPythonExe == null) { return; }

        var python_exe = Path.Join(pathToPythonExe, "python.exe");
        cmake.AddArgument("PYTHON_EXECUTABLE:FILEPATH", python_exe);
    }

    public void Install(BuildEnviroment env, Printer print, BuildData data)
    {
    }

    public IEnumerable<string> GetStatus()
    {
        if (pathToPythonExe != null)
        {
            yield return $"PYTHON: {pathToPythonExe}";
        }
        else
        {
            yield return "Couldn't interpret PYTHON";
        }
    }
}


internal class DependencyAssimp : Dependency
{
    private readonly string dependencyFolder;
    private readonly string installFolder;
    private readonly bool useStaticBuild;

    public DependencyAssimp(BuildData data, bool useStaticBuild)
    {
        dependencyFolder = Path.Join(data.DependencyDirectory, "assimp");
        installFolder = Path.Join(dependencyFolder, "cmake-install");
        this.useStaticBuild = useStaticBuild;
    }

    public string GetName()
    {
        return "assimp";
    }

    public void AddCmakeArguments(CMake.CMake cmake)
    {
        cmake.AddArgument("ASSIMP_ROOT_DIR", installFolder);
    }

    public void Install(BuildEnviroment env, Printer print, BuildData data)
    {
        var url = "https://github.com/assimp/assimp/archive/v5.0.1.zip";

        var deps = data.DependencyDirectory;
        var root = dependencyFolder;
        var install = installFolder;
        var generator = env.CreateCmakeGenerator();

        print.Header("Installing dependency assimp");
        var zipFile = Path.Join(deps, "assimp.zip");
        if (false == Directory.Exists(root))
        {
            Core.VerifyDirectoryExists(print, root);
            Core.VerifyDirectoryExists(print, deps);
            print.Info("downloading assimp");
            Core.DownloadFileIfMissing(print, url, zipFile);
            print.Info("extracting assimp");
            Core.ExtractZip(print, zipFile, root);
            var build = Path.Join(root, "cmake-build");
            Core.MoveFiles(print, Path.Join(root, "assimp-5.0.1"), root);

            var project = new CMake.CMake(build, root, generator);
            project.AddArgument("ASSIMP_BUILD_X3D_IMPORTER", "0");
            if (useStaticBuild)
            {
                project.MakeStaticLibrary();
            }
            print.Info($"Installing cmake to {install}");
            project.SetInstallFolder(install);
            Core.VerifyDirectoryExists(print, install);

            project.Configure(print);
            project.Build(print);

            print.Info("Installing assimp");
            project.Install(print);
        }
        else
        {
            print.Info("Assimp build exist, not building again...");
        }
    }

    public IEnumerable<string> GetStatus()
    {
        yield return $"Root: {dependencyFolder}";
        yield return $"Install: {installFolder}";
    }
}

