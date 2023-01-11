namespace Workbench;

public enum DependencyName
{
    // #[serde(rename = "sdl-2.0.8")]
    Sdl2_0_8,

    // #[serde(rename = "sdl-2.0.20")]
    Sdl2_0_20,

    // #[serde(rename = "python")]
    Python,

    // #[serde(rename = "assimp")]
    Assimp,

    // #[serde(rename = "assimp_static")]
    AssimpStatic

    // earlier dependencies were wxWidgets and to a lesser extent: boost and libxml
    // if we need them we should probably replace this whole setup with a package manager
}

public interface Dependency
{
    string get_name();

    // add arguments to the main cmake
    void add_cmake_arguments(CMake.CMake cmake);

    // install the dependency
    public void install(BuildEnviroment env, Printer print, BuildData data);

    // get the status of the dependency
    public IEnumerable<string> status();
}

public static class Dependencies
{
    public static Dependency CreateDependency(DependencyName name, BuildData data)
    {
        return name switch
        {
            DependencyName.Sdl2_0_8 => new DependencySdl2(data, "https://www.libsdl.org/release/SDL2-2.0.8.zip", "SDL2-2.0.8"),
            DependencyName.Sdl2_0_20 => new DependencySdl2(data, "https://www.libsdl.org/release/SDL2-2.0.20.zip", "SDL2-2.0.20"),
            DependencyName.Python => new DependencyPython(),
            DependencyName.Assimp => new DependencyAssimp(data, false),
            DependencyName.AssimpStatic => new DependencyAssimp(data, true),
            _ => throw new Exception($"invalid name: {name}"),
        };
    }
}

internal class DependencySdl2 : Dependency
{
    string root_folder;
    string build_folder;
    string url;
    string folder_name;

    public DependencySdl2(BuildData data, string zip_file, string folder_name)
    {
        root_folder = Path.Combine(data.dependency_dir, folder_name);
        build_folder = Path.Combine(root_folder, "cmake-build");
        url = zip_file;
        this.folder_name = folder_name;
    }

    public string get_name()
    {
        return "sdl2";
    }

    public void add_cmake_arguments(CMake.CMake cmake)
    {
        cmake.add_argument("SDL2_HINT_ROOT", this.root_folder);
        cmake.add_argument("SDL2_HINT_BUILD", this.build_folder);
    }

    public void install(BuildEnviroment env, Printer print, BuildData data)
    {
        var deps = data.dependency_dir;
        var root = this.root_folder;
        var build = this.build_folder;
        var generator = env.get_cmake_generator();

        print.header("Installing dependency sdl2");

        var zip_file = Path.Join(deps, $"{this.folder_name}.zip");

        if (false == File.Exists(zip_file))
        {
            Core.verify_dir_exist(print, root);
            Core.verify_dir_exist(print, deps);
            print.info("downloading sdl2");
            Core.download_file(print, this.url, zip_file);
        }
        else
        {
            print.info("SDL2 zip file exist, not downloading again...");
        }

        if (false == File.Exists(Path.Join(root, "INSTALL.txt")))
        {
            Core.extract_zip(print, zip_file, root);
            Core.move_files(print, Path.Join(root, this.folder_name), root);
        }
        else
        {
            print.info("SDL2 is unzipped, not unzipping again");
        }

        if (false == File.Exists(Path.Join(build, "SDL2.sln")))
        {
            var project = new CMake.CMake(build, root, generator);
            // project.make_static_library()
            // this is defined by the standard library so don't add it
            // generates '__ftol2_sse already defined' errors
            project.add_argument("LIBC", "ON");
            project.add_argument("SDL_STATIC", "ON");
            project.add_argument("SDL_SHARED", "OFF");
            project.config(print);
            project.build(print);
        }
        else
        {
            print.info("SDL2 build exist, not building again...");
        }
    }

    public IEnumerable<string> status()
    {
        yield return $"Root: {this.root_folder}";
        yield return $"Build: {this.build_folder}";
    }
}

///////////////////////////////////////////////////////////////////////////////////////////////////

internal class DependencyPython : Dependency
{
    readonly string? python_env;

    internal DependencyPython()
    {
        python_env = Environment.GetEnvironmentVariable("PYTHON");
    }

    public string get_name()
    {
        return "python";
    }

    public void add_cmake_arguments(CMake.CMake cmake)
    {
        if (this.python_env == null) { return; }

        var python_exe = Path.Join(python_env, "python.exe");
        cmake.add_argument("PYTHON_EXECUTABLE:FILEPATH", python_exe);
    }

    public void install(BuildEnviroment env, Printer print, BuildData data)
    {
    }

    public IEnumerable<string> status()
    {
        if (python_env != null)
        {
            yield return $"PYTHON: {python_env}";
        }
        else
        {
            yield return "Couldn't interpret PYTHON";
        }
    }
}


internal class DependencyAssimp : Dependency
{
    string assimp_folder;
    string assimp_install_folder;
    bool use_static;

    public DependencyAssimp(BuildData data, bool use_static)
    {
        assimp_folder = Path.Join(data.dependency_dir, "assimp");
        assimp_install_folder = Path.Join(assimp_folder, "cmake-install");
        this.use_static = use_static;
    }

    public string get_name()
    {
        return "assimp";
    }

    public void add_cmake_arguments(CMake.CMake cmake)
    {
        cmake.add_argument("ASSIMP_ROOT_DIR", this.assimp_install_folder);
    }

    public void install(BuildEnviroment env, Printer print, BuildData data)
    {
        var url = "https://github.com/assimp/assimp/archive/v5.0.1.zip";

        var deps = data.dependency_dir;
        var root = this.assimp_folder;
        var install = this.assimp_install_folder;
        var generator = env.get_cmake_generator();

        print.header("Installing dependency assimp");
        var zip_file = Path.Join(deps, "assimp.zip");
        if (false == Directory.Exists(root))
        {
            Core.verify_dir_exist(print, root);
            Core.verify_dir_exist(print, deps);
            print.info("downloading assimp");
            Core.download_file(print, url, zip_file);
            print.info("extracting assimp");
            Core.extract_zip(print, zip_file, root);
            var build = Path.Join(root, "cmake-build");
            Core.move_files(print, Path.Join(root, "assimp-5.0.1"), root);

            var project = new CMake.CMake(build, root, generator);
            project.add_argument("ASSIMP_BUILD_X3D_IMPORTER", "0");
            if (this.use_static)
            {
                project.make_static_library();
            }
            print.info($"Installing cmake to {install}");
            project.set_install_folder(install);
            Core.verify_dir_exist(print, install);

            project.config(print);
            project.build(print);

            print.info("Installing assimp");
            project.install(print);
        }
        else
        {
            print.info("Assimp build exist, not building again...");
        }
    }

    public IEnumerable<string> status()
    {
        yield return $"Root: {this.assimp_folder}";
        yield return $"Install: {this.assimp_install_folder}";
    }
}

