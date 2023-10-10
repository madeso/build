using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Workbench.CMake;
using Workbench.Utils;

namespace Workbench.Build;

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
            DependencyName.WxWidgets=> new DependencyWxWidgets(data, "https://github.com/wxWidgets/wxWidgets/releases/download/v3.2.2.1/wxWidgets-3.2.2.1.zip", "wxWidgets-3-2-2-1"),
            _ => throw new Exception($"invalid name: {name}"),
        };
    }
}

internal class DependencySdl2 : BuildDependency
{
    private readonly string _rootFolder;
    private readonly string _buildFolder;
    private readonly string _url;
    private readonly string _folderName;

    public DependencySdl2(BuildData data, string zipFile, string folderName)
    {
        _rootFolder = Path.Combine(data.DependencyDirectory, folderName);
        _buildFolder = Path.Combine(_rootFolder, "cmake-build");
        _url = zipFile;
        _folderName = folderName;
    }

    public string GetName()
    {
        return "sdl2";
    }

    public void AddCmakeArguments(CMake.CMake cmake)
    {
        cmake.AddArgument("SDL2_HINT_ROOT", _rootFolder);
        cmake.AddArgument("SDL2_HINT_BUILD", _buildFolder);
    }

    public void Install(BuildEnvironment env, Printer print, BuildData data)
    {
        var generator = env.CreateCmakeGenerator();

        print.Header("Installing dependency sdl2");

        var zipFile = Path.Join(data.DependencyDirectory, $"{_folderName}.zip");

        if (false == File.Exists(zipFile))
        {
            Core.VerifyDirectoryExists(print, _rootFolder);
            Core.VerifyDirectoryExists(print, data.DependencyDirectory);
            print.Info("downloading sdl2");
            Core.DownloadFileIfMissing(print, _url, zipFile);
        }
        else
        {
            print.Info("SDL2 zip file exist, not downloading again...");
        }

        if (false == File.Exists(Path.Join(_rootFolder, "INSTALL.txt")))
        {
            Core.ExtractZip(print, zipFile, _rootFolder);
            Core.MoveFiles(print, Path.Join(_rootFolder, _folderName), _rootFolder);
        }
        else
        {
            print.Info("SDL2 is unzipped, not unzipping again");
        }

        if (false == File.Exists(Path.Join(_buildFolder, "SDL2.sln")))
        {
            var project = new CMake.CMake(_buildFolder, _rootFolder, generator);
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
        yield return $"Root: {_rootFolder}";
        yield return $"Build: {_buildFolder}";
    }
}


///////////////////////////////////////////////////////////////////////////////////////////////////

internal class DependencyPython : BuildDependency
{
    private readonly string? _pathToPythonExe;

    internal DependencyPython()
    {
        _pathToPythonExe = Environment.GetEnvironmentVariable("PYTHON");
    }

    public string GetName()
    {
        return "python";
    }

    public void AddCmakeArguments(CMake.CMake cmake)
    {
        if (_pathToPythonExe == null) { return; }

        var pythonExe = Path.Join(_pathToPythonExe, "python.exe");
        cmake.AddArgument("PYTHON_EXECUTABLE:FILEPATH", pythonExe);
    }

    public void Install(BuildEnvironment env, Printer print, BuildData data)
    {
    }

    public IEnumerable<string> GetStatus()
    {
        if (_pathToPythonExe != null)
        {
            yield return $"PYTHON: {_pathToPythonExe}";
        }
        else
        {
            yield return "Couldn't interpret PYTHON";
        }
    }
}


internal class DependencyAssimp : BuildDependency
{
    private readonly string _dependencyFolder;
    private readonly string _installFolder;
    private readonly bool _useStaticBuild;

    public DependencyAssimp(BuildData data, bool useStaticBuild)
    {
        _dependencyFolder = Path.Join(data.DependencyDirectory, "assimp");
        _installFolder = Path.Join(_dependencyFolder, "cmake-install");
        this._useStaticBuild = useStaticBuild;
    }

    public string GetName()
    {
        return "assimp";
    }

    public void AddCmakeArguments(CMake.CMake cmake)
    {
        cmake.AddArgument("ASSIMP_ROOT_DIR", _installFolder);
    }

    public void Install(BuildEnvironment env, Printer print, BuildData data)
    {
        const string url = "https://github.com/assimp/assimp/archive/v5.0.1.zip";

        var generator = env.CreateCmakeGenerator();

        print.Header("Installing dependency assimp");
        var zipFile = Path.Join(data.DependencyDirectory, "assimp.zip");
        if (false == Directory.Exists(_dependencyFolder))
        {
            Core.VerifyDirectoryExists(print, _dependencyFolder);
            Core.VerifyDirectoryExists(print, data.DependencyDirectory);
            print.Info("downloading assimp");
            Core.DownloadFileIfMissing(print, url, zipFile);
            print.Info("extracting assimp");
            Core.ExtractZip(print, zipFile, _dependencyFolder);
            var build = Path.Join(_dependencyFolder, "cmake-build");
            Core.MoveFiles(print, Path.Join(_dependencyFolder, "assimp-5.0.1"), _dependencyFolder);

            var project = new CMake.CMake(build, _dependencyFolder, generator);
            project.AddArgument("ASSIMP_BUILD_X3D_IMPORTER", "0");
            if (_useStaticBuild)
            {
                project.MakeStaticLibrary();
            }
            print.Info($"Installing cmake to {_installFolder}");
            project.SetInstallFolder(_installFolder);
            Core.VerifyDirectoryExists(print, _installFolder);

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
        yield return $"Root: {_dependencyFolder}";
        yield return $"Install: {_installFolder}";
    }
}




internal static class BuildUtils
{
    private static IEnumerable<string> ListProjectsInSolution(string path)
    {
        var directoryName = new DirectoryInfo(path).Name;
        var projectLine = new Regex("""Project\("[^"]+"\) = "[^"]+", "([^"]+)" """.TrimEnd()); // possible to end a rawy string literal with a quote?
        // with open(path) as sln
        {
            foreach (var line in File.ReadAllLines(path))
            {
                // Project("{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}") = "richtext", "wx_richtext.vcxproj", "{7FB0902D-8579-5DCE-B883-DAF66A885005}"
                var projectMatch = projectLine.Match(line);
                if (projectMatch.Success)
                {
                    yield return Path.Join(directoryName, projectMatch.Groups[1].Value);
                }
            }
        }
    }




    internal static void ChangeAllProjectsToStatic(Printer printer, string sln)
    {
        var projects = ListProjectsInSolution(sln);
        foreach (var proj in projects)
        {
            ChangeToStaticLink(printer, proj);
        }
    }


    private static void AddDefinitionToSolution(string sln, string definition)
    {
        var projects = ListProjectsInSolution(sln);
        foreach (var proj in projects)
        {
            AddDefinitionToProject(proj, definition);
        }
    }


    private static void MakeSingleProject64(Printer print, string projectPath, TextReplacer rep)
    {
        if (!Path.Exists(projectPath))
        {
            print.Error("missing " + projectPath);
            return;
        }

        var lines = File.ReadLines(projectPath).Select(line => rep.Replace(line.TrimEnd()));
        File.WriteAllLines(projectPath, lines.ToArray());
    }


    private static void MakeProjects64(Printer print, string sln)
    {
        var projects = ListProjectsInSolution(sln);
        var rep = new TextReplacer();
        rep.Add("Win32", "x64");
        rep.Add("<DebugInformationFormat>EditAndContinue</DebugInformationFormat>", "<DebugInformationFormat>ProgramDatabase</DebugInformationFormat>");
        rep.Add("<TargetMachine>MachineX86</TargetMachine>", "<TargetMachine>MachineX64</TargetMachine>");
        // protobuf specific hack since cmake looks in x64 folder
        rep.Add(@"<OutDir>Release\</OutDir>", @"<OutDir>x64\Release\</OutDir>");
        rep.Add(@"<OutDir>Debug\</OutDir>", @"<OutDir>x64\Debug\</OutDir>");
        foreach (var project in projects)
        {
            MakeSingleProject64(print, project, rep);
        }
    }


    private static void MakeSolution64(string solutionPath)
    {
        var rep = new TextReplacer();
        rep.Add("Win32", "x64");

        var lines = new List<string>();

        //with open(solution_path) as slnlines
        foreach (var line in File.ReadLines(solutionPath))
        {
            var newLine = rep.Replace(line.TrimEnd());
            lines.Add(newLine);
        }

        File.WriteAllLines(solutionPath, lines.ToArray());
    }


    private static void ConvertSlnTo64(Printer print, string sln)
    {
        MakeSolution64(sln);
        MakeProjects64(print, sln);
    }

    private static void AddDefinitionToProject(string path, string define)
    {
        // <PreprocessorDefinitions>WIN32;_LIB;_CRT_SECURE_NO_DEPRECATE=1;_CRT_NON_CONFORMING_SWPRINTFS=1;_SCL_SECURE_NO_WARNINGS=1;__WXMSW__;NDEBUG;_UNICODE;WXBUILDING;%(PreprocessorDefinitions)</PreprocessorDefinitions>
        var preproc = new Regex(@"([ ]*<PreprocessorDefinitions>)([^<]*</PreprocessorDefinitions>)");
        var lines = new List<string>();

        foreach (var line in File.ReadAllLines(path))
        {
            var preprocMatch = preproc.Match(line);
            if (preprocMatch.Success)
            {
                var before = preprocMatch.Groups[1].Value;
                var after = preprocMatch.Groups[2].Value;
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
    private static void ChangeToStaticLink(Printer print, string path)
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
    private readonly string _rootFolder;
    private readonly string _buildFolder;
    private readonly string _url;
    private readonly string _folderName;

    public DependencyWxWidgets(BuildData data, string zipFile, string folderName)
    {
        _rootFolder = Path.Combine(data.DependencyDirectory, folderName);
        _buildFolder = Path.Combine(_rootFolder, "cmake-build");
        _url = zipFile;
        _folderName = folderName;
    }

    public string GetName()
    {
        return "wxWidgets";
    }

    public void AddCmakeArguments(CMake.CMake cmake)
    {
        // if these differs it clears the lib dir... but also one is required to use / on windows... wtf!
        cmake.AddArgument("WX_ROOT_DIR", _rootFolder.Replace('\\', '/'));
        cmake.AddArgument("wxWidgets_ROOT_DIR", _rootFolder);
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
        => Path.Join(_buildFolder, "lib", "vc_x64_lib");

    public void Install(BuildEnvironment env, Printer print, BuildData data)
    {
        var generator = env.CreateCmakeGenerator();

        print.Header("Installing dependency wxwidgets");

        var zipFile = Path.Join(data.DependencyDirectory, $"{_folderName}.zip");

        if (false == File.Exists(zipFile))
        {
            Core.VerifyDirectoryExists(print, _rootFolder);
            Core.VerifyDirectoryExists(print, data.DependencyDirectory);
            print.Info("downloading wxwidgets");
            Core.DownloadFileIfMissing(print, _url, zipFile);
        }
        else
        {
            print.Info("wxWidgets zip file exist, not downloading again...");
        }

        if (false == File.Exists(Path.Join(_rootFolder, "CMakeLists.txt")))
        {
            Core.ExtractZip(print, zipFile, _rootFolder);
        }
        else
        {
            print.Info("wxWidgets is unzipped, not unzipping again");
        }

        var buildDbg = false == File.Exists(Path.Join(GetLibraryFolder(), "wxzlibd.lib"));
        var buildRel = false == File.Exists(Path.Join(GetLibraryFolder(), "wxzlib.lib"));
        
        if (buildDbg || buildRel)
        {
            var project = ConfigProject(print, _rootFolder, _buildFolder, generator);
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
        yield return $"Root: {_rootFolder}";
        yield return $"Build: {_buildFolder}";
    }
}
