using System.Collections.Immutable;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Workbench.Utils;
using static Workbench.Commands.IndentCommands.IndentationCommand;

namespace Workbench.CMake;

internal static class CmakeTools
{
    private const string CmakeCacheFile = "CMakeCache.txt";

    private static Found FindInstallationInRegistry(Printer printer)
    {
        var registry_source = "registry";

        var install_dir = Registry.Hklm(@"SOFTWARE\Kitware\CMake", "InstallDir");
        if (install_dir == null) { return new Found(null, registry_source); }

        var path = Path.Join(install_dir, "bin", "cmake.exe");
        if (File.Exists(path) == false)
        {
            printer.Error($"Found path to cmake in registry ({path}) but it didn't exist");
            return new Found(null, registry_source);
        }

        return new Found(path, registry_source);
    }


    private static Found FindInstallationInPath(Printer printer)
    {
        var path_source = "path";
        var path = Which.Find("cmake");
        if (path == null)
        {
            return new Found(null, path_source);
        }

        if (File.Exists(path) == false)
        {
            printer.Error($"Found path to cmake in path ({path}) but it didn't exist");
            return new Found(null, path_source);
        }
        return new Found(path, path_source);
    }


    public static IEnumerable<Found> ListAllInstallations(Printer printer)
    {
        yield return FindInstallationInRegistry(printer);
        yield return FindInstallationInPath(printer);
    }


    public static string? FindInstallationOrNull(Printer printer)
    {
        return Found.GetFirstValueOrNull(ListAllInstallations(printer));
    }


    public static Found FindBuildInCurrentDirectory()
    {
        var source = "current dir";

        var build_root = new DirectoryInfo(Environment.CurrentDirectory).FullName;
        if (new FileInfo(Path.Join(build_root, CmakeCacheFile)).Exists == false)
        {
            return new Found(null, source);
        }

        return new Found(build_root, source);
    }

    public static Found FindBuildFromCompileCommands(CompileCommands.CommonArguments settings, Printer printer)
    {
        var found = settings.GetPathToCompileCommandsOrNull(printer);
        return new Found(found, "compile commands");
    }

    public static Found FindSingleBuildWithCache()
    {
        var cwd = Environment.CurrentDirectory;
        var roots = FileUtil.PitchforkBuildFolders(cwd)
            .Where(root => new FileInfo(Path.Join(root, CmakeCacheFile)).Exists)
            .ToImmutableArray();

        return roots.Length switch
        {
            0 => new Found(null, "no builds found from cache"),
            1 => new Found(roots[0], "build root with cache"),
            _ => new Found(null, "too many builds found from cache"),
        };
    }

    public static IEnumerable<Found> ListAllBuilds(CompileCommands.CommonArguments settings, Printer printer)
    {
        yield return FindBuildInCurrentDirectory();
        yield return FindBuildFromCompileCommands(settings, printer);
        yield return FindSingleBuildWithCache();
    }


    public static string? FindBuildOrNone(CompileCommands.CommonArguments settings, Printer printer)
    {
        return Found.GetFirstValueOrNull(ListAllBuilds(settings, printer));
    }
}

// a cmake argument
public class Argument
{
    public Argument(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public Argument(string name, string value, string typename) : this(name, value)
    {
        TypeName = typename;
    }

    // format for commandline
    public string FormatForCmakeArgument()
    {
        if (TypeName == null)
        {
            return $"-D{Name}={Value}";
        }
        else
        {
            return $"-D{Name}:{TypeName}={Value}";
        }
    }

    public string Name { get; }
    public string Value { get; }
    public string? TypeName { get; }
}


// cmake generator
public class Generator
{
    public Generator(string name, string? arch = null)
    {
        Name = name;
        Arch = arch;
    }

    public string Name { get; }
    public string? Arch { get; }
}

public enum Config
{
    Debug, Releaase
}

public enum Install
{
    No, Yes
}

// utility to call cmake commands on a project
public class CMake
{
    public CMake(string buildFolder, string sourceFolder, Generator generator)
    {
        this.generator = generator;
        this.buildFolder = buildFolder;
        this.sourceFolder = sourceFolder;
    }

    private readonly Generator generator;
    private readonly string buildFolder;
    private readonly string sourceFolder;
    private readonly List<Argument> arguments = new();


    // add argument with a explicit type set
    private void AddArgumentWithType(string name, string value, string typename)
    {
        arguments.Add(new Argument(name, value, typename));
    }

    // add argument
    public void AddArgument(string name, string value)
    {
        arguments.Add(new Argument(name, value));
    }

    // set the install folder
    public void SetInstallFolder(string folder)
    {
        AddArgumentWithType("CMAKE_INSTALL_PREFIX", folder, "PATH");
    }

    // set cmake to make static (not shared) library
    public void MakeStaticLibrary()
    {
        AddArgument("BUILD_SHARED_LIBS", "0");
    }

    // run cmake configure step
    public void Configure(Printer printer, bool nop = false)
    {
        var cmake = CmakeTools.FindInstallationOrNull(printer);
        if (cmake == null)
        {
            printer.Error("CMake executable not found");
            return;
        }

        var command = new ProcessBuilder(cmake);
        foreach (var arg in arguments)
        {
            var argument = arg.FormatForCmakeArgument();
            printer.Info($"Setting CMake argument for config: {argument}");
            command.AddArgument(argument);
        }

        command.AddArgument(sourceFolder);
        command.AddArgument("-G");
        command.AddArgument(generator.Name);
        if (generator.Arch != null)
        {
            command.AddArgument("-A");
            command.AddArgument(generator.Arch);
        }

        Core.VerifyDirectoryExists(printer, buildFolder);
        command.WorkingDirectory = buildFolder;

        if (Core.IsWindows())
        {
            if (nop)
            {
                printer.Info($"Configuring cmake: {command}");
            }
            else
            {
                command.RunAndPrintOutput(printer);
            }
        }
        else
        {
            printer.Info($"Configuring cmake: {command}");
        }
    }

    // run cmake build step
    private void RunBuildCommand(Printer printer, Install install, Config config)
    {
        var cmake = CmakeTools.FindInstallationOrNull(printer);
        if (cmake == null)
        {
            printer.Error("CMake executable not found");
            return;
        }

        var command = new ProcessBuilder(cmake);
        command.AddArgument("--build");
        command.AddArgument(".");

        if (install == Workbench.CMake.Install.Yes)
        {
            command.AddArgument("--target");
            command.AddArgument("install");
        }
        command.AddArgument("--config");
        command.AddArgument(config switch
        {
            Config.Debug => "Debug",
            Config.Releaase => "Release",
            _ => throw new NotImplementedException(),
        });

        Core.VerifyDirectoryExists(printer, buildFolder);
        command.WorkingDirectory = buildFolder;

        if (Core.IsWindows())
        {
            command.RunAndPrintOutput(printer);
        }
        else
        {
            printer.Info($"Found path to cmake in path ({command}) but it didn't exist");
        }
    }

    // build cmake project
    public void Build(Printer printer, Config config)
    {
        RunBuildCommand(printer, Workbench.CMake.Install.No, config);
    }

    // install cmake project
    public void Install(Printer printer, Config config)
    {
        RunBuildCommand(printer, Workbench.CMake.Install.Yes, config);
    }
}

public class Trace
{
    [JsonPropertyName("file")]
    public string File { set; get; } = string.Empty;

    [JsonPropertyName("line")]
    public int Line { set; get; }

    [JsonPropertyName("cmd")]
    public string Cmd { set; get; } = string.Empty;

    [JsonPropertyName("args")]
    public string[] Args { set; get; } = Array.Empty<string>();

    public static IEnumerable<Trace> TraceDirectory(string cmakeExecutable, string dir)
    {
        List<Trace> lines = new();
        List<string> error = new();

        void on_line(string src)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Trace>(src);
                if (parsed != null && parsed.File != null)
                {
                    // file != null ignores the version json object
                    lines.Add(parsed);
                }
                else
                {
                    error.Add($"{src}: null object after parsing");
                }
            }
            catch (JsonException ex)
            {
                error.Add($"{src}: {ex.Message}");
            }
            catch (NotSupportedException ex)
            {
                error.Add($"{src}: {ex.Message}");
            }
        }

        var ret = new ProcessBuilder(cmakeExecutable, "--trace-expand", "--trace-format=json-v1", "-S", Environment.CurrentDirectory, "-B", dir)
            .InDirectory(dir)
            .RunWithCallback(on_line, err => error.Add(err))
            .ExitCode
            ;

        if (ret != 0)
        {
            var mess = string.Join('\n', error);
            throw new TraceError($"{error.Count} -> {mess}");
        }

        return lines;
    }

    
    public IEnumerable<string> ListFilesInCmakeLibrary()
    {
        return RunFileList("STATIC");
    }

    public IEnumerable<string> ListFilesInCmakeExecutable()
    {
        return RunFileList("WIN32", "MACOSX_BUNDLE");
    }

    private IEnumerable<string> RunFileList(params string[] ignoreableArguments)
    {
        var cmake = this;
        var args = cmake.Args.Skip(1).ToImmutableArray();
        args = Args
            .Skip(1) // name of library/app
            .SkipWhile(arg => ignoreableArguments.Contains(arg))
            .ToImmutableArray();
        var folder = new FileInfo(cmake.File).Directory?.FullName!;

        foreach (var a in args)
        {
            foreach (var f in a.Split(';'))
            {
                yield return new FileInfo(Path.Join(folder, f)).FullName;
            }
        }
    }
}

[Serializable]
internal class TraceError : Exception
{
    public TraceError()
    {
    }

    public TraceError(string message) : base(message)
    {
    }

    public TraceError(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    protected TraceError(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}

