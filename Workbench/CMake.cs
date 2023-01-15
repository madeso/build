using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Workbench.CMake;

internal static class CmakeTools
{
    private static Found FindnRegistry(Printer printer)
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


    private static Found FindInPath(Printer printer)
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


    public static IEnumerable<Found> ListAll(Printer printer)
    {
        yield return FindnRegistry(printer);
        yield return FindInPath(printer);
    }


    public static string? FindOrNull(Printer printer)
    {
        return Found.GetFirstValueOrNull(ListAll(printer));
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
        var cmake = CmakeTools.FindOrNull(printer);
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
                command.RunAndGetOutput().PrintStatusAndUpdate(printer);
            }
        }
        else
        {
            printer.Info($"Configuring cmake: {command}");
        }
    }

    // run cmake build step
    private void RunBuildCommand(Printer printer, bool install)
    {
        var cmake = CmakeTools.FindOrNull(printer);
        if (cmake == null)
        {
            printer.Error("CMake executable not found");
            return;
        }

        var command = new ProcessBuilder(cmake);
        command.AddArgument("--build");
        command.AddArgument(".");

        if (install)
        {
            command.AddArgument("--target");
            command.AddArgument("install");
        }
        command.AddArgument("--config");
        command.AddArgument("Release");

        Core.VerifyDirectoryExists(printer, buildFolder);
        command.WorkingDirectory = buildFolder;

        if (Core.IsWindows())
        {
            command.RunAndGetOutput().PrintStatusAndUpdate(printer);
        }
        else
        {
            printer.Info($"Found path to cmake in path ({command}) but it didn't exist");
        }
    }

    // build cmake project
    public void Build(Printer printer)
    {
        RunBuildCommand(printer, false);
    }

    // install cmake project
    public void Install(Printer printer)
    {
        RunBuildCommand(printer, true);
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

    public static IEnumerable<Trace> TraceDirectory(string dir)
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

        var ret = new ProcessBuilder("cmake", "--trace-format=json-v1")
            .InDirectory(dir)
            .RunWithCallback(on_line)
            .ExitCode
            ;

        if (ret != 0)
        {
            var mess = string.Join('\n', error);
            throw new TraceError($"{error.Count} -> {mess}");
        }

        return lines;
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

