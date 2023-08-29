using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Workbench.CMake;
using Workbench.Utils;
using static Workbench.Commands.IndentCommands.IndentationCommand;
using static Workbench.Doxygen.Compound.linkedTextType;

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
    void AddCmakeArguments(CMake.CMake cmake);

    // install the dependency
    public void Install(BuildEnviroment env, Printer print, BuildData data);

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
            DependencyName.WxWidgets=> new DependencyWxWidgets(data, "https://github.com/wxWidgets/wxWidgets/releases/download/v3.2.2.1/wxWidgets-3.2.2.1.zip", "wxWidgets-3-2-2-1"),
            _ => throw new Exception($"invalid name: {name}"),
        };
    }
}

internal class DependencySdl2 : BuildDependency
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
            project.Build(print, CMake.Config.Releaase);
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

internal class DependencyPython : BuildDependency
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


internal class DependencyAssimp : BuildDependency
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
            project.Build(print, CMake.Config.Releaase);

            print.Info("Installing assimp");
            project.Install(print, CMake.Config.Releaase);
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




internal static class BuildUtils
{
    private static IEnumerable<string> list_projects_in_solution(string path)
    {
        var directory_name = new DirectoryInfo(path).Name;
        var project_line = new Regex("""Project\("[^"]+"\) = "[^"]+", "([^"]+)" """.TrimEnd()); // possible to end a rawy string literal with a quote?
        // with open(path) as sln
        {
            foreach (var line in File.ReadAllLines(path))
            {
                // Project("{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}") = "richtext", "wx_richtext.vcxproj", "{7FB0902D-8579-5DCE-B883-DAF66A885005}"
                var project_match = project_line.Match(line);
                if (project_match.Success)
                {
                    yield return Path.Join(directory_name, project_match.Groups[1].Value);
                }
            }
        }
    }




    internal static void change_all_projects_to_static(Printer printer, string sln)
    {
        var projects = list_projects_in_solution(sln);
        foreach (var proj in projects)
        {
            change_to_static_link(printer, proj);
        }
    }


    private static void add_definition_to_solution(string sln, string definition)
    {
        var projects = list_projects_in_solution(sln);
        foreach (var proj in projects)
        {
            add_definition_to_project(proj, definition);
        }
    }


    private static void make_single_project_64(Printer print, string project_path, TextReplacer rep)
    {
        if (!Path.Exists(project_path))
        {
            print.Error("missing " + project_path);
            return;
        }

        var lines = new List<string>();
        foreach (var line in File.ReadLines(project_path))
        {
            var new_line = rep.Replace(line.TrimEnd());
            lines.Add(new_line);
        }

        File.WriteAllLines(project_path, lines.ToArray());
    }


    private static void make_projects_64(Printer print, string sln)
    {
        var projects = list_projects_in_solution(sln);
        var rep = new TextReplacer();
        rep.Add("Win32", "x64");
        rep.Add("<DebugInformationFormat>EditAndContinue</DebugInformationFormat>", "<DebugInformationFormat>ProgramDatabase</DebugInformationFormat>");
        rep.Add("<TargetMachine>MachineX86</TargetMachine>", "<TargetMachine>MachineX64</TargetMachine>");
        // protobuf specific hack since cmake looks in x64 folder
        rep.Add(@"<OutDir>Release\</OutDir>", @"<OutDir>x64\Release\</OutDir>");
        rep.Add(@"<OutDir>Debug\</OutDir>", @"<OutDir>x64\Debug\</OutDir>");
        foreach (var project in projects)
        {
            make_single_project_64(print, project, rep);
        }
    }


    private static void make_solution_64(string solution_path)
    {
        var rep = new TextReplacer();
        rep.Add("Win32", "x64");

        var lines = new List<string>();

        //with open(solution_path) as slnlines
        {
            foreach (var line in File.ReadLines(solution_path))
            {
                var new_line = rep.Replace(line.TrimEnd());
                lines.Add(new_line);
            }
        }

        File.WriteAllLines(solution_path, lines.ToArray());
    }


    private static void convert_sln_to_64(Printer print, string sln)
    {
        make_solution_64(sln);
        make_projects_64(print, sln);
    }

    private static void add_definition_to_project(string path, string define)
    {
        // <PreprocessorDefinitions>WIN32;_LIB;_CRT_SECURE_NO_DEPRECATE=1;_CRT_NON_CONFORMING_SWPRINTFS=1;_SCL_SECURE_NO_WARNINGS=1;__WXMSW__;NDEBUG;_UNICODE;WXBUILDING;%(PreprocessorDefinitions)</PreprocessorDefinitions>
        var preproc = new Regex(@"([ ]*<PreprocessorDefinitions>)([^<]*</PreprocessorDefinitions>)");
        var lines = new List<string>();

        foreach (var line in File.ReadAllLines(path))
        {
            var preproc_match = preproc.Match(line);
            if (preproc_match.Success)
            {
                var before = preproc_match.Groups[1].Value;
                var after = preproc_match.Groups[2].Value;
                lines.Add($"{before}{define};{after}");
            }
            else
            {
                lines.Add(line.TrimEnd());
            }
        }

        File.WriteAllLines(path, lines.ToArray());
    }

    // change from:
    // <RuntimeLibrary>MultiThreadedDebugDLL</RuntimeLibrary> to <RuntimeLibrary>MultiThreadedDebug</RuntimeLibrary>
    // <RuntimeLibrary>MultiThreadedDLL</RuntimeLibrary> to <RuntimeLibrary>MultiThreaded</RuntimeLibrary>
    private static void change_to_static_link(Printer print, string path)
    {
        var mtdebug = new Regex(@"([ ]*)<RuntimeLibrary>MultiThreadedDebugDLL");
        var mtrelease = new Regex(@"([ ]*)<RuntimeLibrary>MultiThreadedDLL");
        var lines = new List<string>();

        foreach (var line in File.ReadAllLines(path))
        {
            var mdebug = mtdebug.Match(line);
            var mrelease = mtrelease.Match(line);
            if (mdebug.Success)
            {
                print.Info($"in {path} changed to static debug");
                var spaces = mdebug.Groups[1].Value;
                lines.Add($"{spaces}<RuntimeLibrary>MultiThreadedDebug</RuntimeLibrary>");
            }
            else if (mrelease.Success)
            {
                print.Info($"in {path} changed to static release");
                var spaces = mrelease.Groups[1].Value;
                lines.Add($"{spaces}<RuntimeLibrary>MultiThreaded</RuntimeLibrary>");
            }
            else
            {
                lines.Add(line.TrimEnd());
            }
        }

        File.WriteAllLines(path, lines.ToArray());
    }
}



internal class DependencyWxWidgets : BuildDependency
{
    private readonly string root_folder;
    private readonly string build_folder;
    private readonly string url;
    private readonly string folderName;

    public DependencyWxWidgets(BuildData data, string zipFile, string folderName)
    {
        root_folder = Path.Combine(data.DependencyDirectory, folderName);
        build_folder = Path.Combine(root_folder, "cmake-build");
        url = zipFile;
        this.folderName = folderName;
    }

    public string GetName()
    {
        return "wxWidgets";
    }

    public void AddCmakeArguments(CMake.CMake cmake)
    {
        // if theese differs it clears the lib dir... but also one is required to use / on windows... wtf!
        cmake.AddArgument("WX_ROOT_DIR", root_folder.Replace('\\', '/'));
        cmake.AddArgument("wxWidgets_ROOT_DIR", root_folder);
        cmake.AddArgument("wxWidgets_CONFIGURATION", "mswu");

        cmake.AddArgument("wxWidgets_USE_REL_AND_DBG", "ON"); // require both debug and release

        // perhaps replae \ with /
        string p = GetLibraryFolder();
        p = p.Replace('\\', '/');
        if (p.EndsWith('/') == false) { p += '/'; }
        cmake.AddArgument("wxWidgets_LIB_DIR", p);
    }

    // todo(Gustav): switch this when building 32 bit
    private string GetLibraryFolder() => Path.Join(build_folder, "lib", "vc_x64_lib");

    public void Install(BuildEnviroment env, Printer print, BuildData data)
    {
        var deps = data.DependencyDirectory;
        var root = root_folder;
        var build = build_folder;
        var generator = env.CreateCmakeGenerator();

        print.Header("Installing dependency wxwidgets");

        var zip_file = Path.Join(deps, $"{folderName}.zip");

        if (false == File.Exists(zip_file))
        {
            Core.VerifyDirectoryExists(print, root);
            Core.VerifyDirectoryExists(print, deps);
            print.Info("downloading wxwidgets");
            Core.DownloadFileIfMissing(print, url, zip_file);
        }
        else
        {
            print.Info("wxWidgets zip file exist, not downloading again...");
        }

        if (false == File.Exists(Path.Join(root, "CMakeLists.txt")))
        {
            Core.ExtractZip(print, zip_file, root);
        }
        else
        {
            print.Info("wxWidgets is unzipped, not unzipping again");
        }

        bool buildDbg = false == File.Exists(Path.Join(GetLibraryFolder(), "wxzlibd.lib"));
        bool buildRel = false == File.Exists(Path.Join(GetLibraryFolder(), "wxzlib.lib"));
        
        if (buildDbg || buildRel)
        {
            CMake.CMake project = ConfigProject(print, root, build, generator);
            if(buildDbg)
            {
                print.Info("building debug wxWidgets");
                project.Build(print, CMake.Config.Debug);
            }

            if(buildRel)
            {
                print.Info("building release wxWidgets");
                project.Build(print, CMake.Config.Releaase);
            }
        }
        else
        {
            print.Info("wxWidgets build exist, not building again...");
        }
    }

    private static CMake.CMake ConfigProject(Printer print, string root, string build, Generator generator)
    {
        var project = new CMake.CMake(build, root, generator);
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
