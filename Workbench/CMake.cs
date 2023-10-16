using System.Collections.Immutable;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Workbench.Utils;

namespace Workbench.CMake;

internal static class CmakeTools
{
    private const string CmakeCacheFile = "CMakeCache.txt";

    private static Found FindInstallationInRegistry(Printer printer)
    {
        const string registrySource = "registry";

        var installDir = Registry.Hklm(@"SOFTWARE\Kitware\CMake", "InstallDir");
        if (installDir == null) { return new Found(null, registrySource); }

        var path = Path.Join(installDir, "bin", "cmake.exe");
        if (File.Exists(path) == false)
        {
            printer.Error($"Found path to cmake in registry ({path}) but it didn't exist");
            return new Found(null, registrySource);
        }

        return new Found(path, registrySource);
    }


    private static Found FindInstallationInPath(Printer printer)
    {
        const string pathSource = "path";
        var path = Which.Find("cmake");
        if (path == null)
        {
            return new Found(null, pathSource);
        }

        if (File.Exists(path) == false)
        {
            printer.Error($"Found path to cmake in path ({path}) but it didn't exist");
            return new Found(null, pathSource);
        }
        return new Found(path, pathSource);
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

        var buildRoot = new DirectoryInfo(Environment.CurrentDirectory).FullName;
        if (new FileInfo(Path.Join(buildRoot, CmakeCacheFile)).Exists == false)
        {
            return new Found(null, source);
        }

        return new Found(buildRoot, source);
    }

    public static Found FindBuildFromCompileCommands(CompileCommandsArguments settings, Printer printer)
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

    public static IEnumerable<Found> ListAllBuilds(CompileCommandsArguments settings, Printer printer)
    {
        yield return FindBuildInCurrentDirectory();
        yield return FindBuildFromCompileCommands(settings, printer);
        yield return FindSingleBuildWithCache();
    }


    public static string? FindBuildOrNone(CompileCommandsArguments settings, Printer printer)
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
    Debug, Release
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
        foreach (var argument in arguments.Select(arg => arg.FormatForCmakeArgument()))
        {
            Printer.Info($"Setting CMake argument for config: {argument}");
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
                Printer.Info($"Configuring cmake: {command}");
            }
            else
            {
                command.RunAndPrintOutput(printer);
            }
        }
        else
        {
            Printer.Info($"Configuring cmake: {command}");
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
            Config.Release => "Release",
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
            Printer.Info($"Found path to cmake in path ({command}) but it didn't exist");
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
    public string? File { set; get; } = string.Empty;

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

        void OnLine(string src)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Trace>(src);
                if (parsed is { File: not null })
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

        var stderr = new List<string>();
        var ret = new ProcessBuilder(cmakeExecutable, "--trace-expand", "--trace-format=json-v1", "-S", Environment.CurrentDirectory, "-B", dir)
            .InDirectory(dir)
            .RunWithCallback(null, OnLine, err => { OnLine(err); stderr.Add(err); }, (err, ex) => { error.Add(err); error.Add(ex.Message);})
            .ExitCode
            ;

        if (ret == 0)
        {
            return lines;
        }

        var stderrMessage = string.Join(Environment.NewLine, stderr).Trim();
        var space = string.IsNullOrEmpty(stderrMessage) ? string.Empty : ": ";
        var errorMessage = string.Join(Environment.NewLine, error);
        throw new TraceError($"{stderrMessage}{space}{error.Count} -> {errorMessage}");

    }

    
    public IEnumerable<string> ListFilesInCmakeLibrary()
    {
        return ListFilesInArgs("STATIC");
    }

    public IEnumerable<string> ListFilesInCmakeExecutable()
    {
        return ListFilesInArgs("WIN32", "MACOSX_BUNDLE");
    }

    private IEnumerable<string> ListFilesInArgs(params string[] argumentsToIgnore)
    {
        var folder = new FileInfo(File!).Directory?.FullName!;

        return Args
                .Skip(1) // name of library/app
                .SkipWhile(argumentsToIgnore.Contains)
                .SelectMany(a => a.Split(';'))
                .Select(f => new FileInfo(Path.Join(folder, f)).FullName)
            ;
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

