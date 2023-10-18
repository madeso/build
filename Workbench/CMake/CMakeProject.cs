using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;
using Workbench.Utils;

namespace Workbench.CMake;

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
public class CMakeProject
{
    public CMakeProject(string build_folder, string source_folder, Generator generator)
    {
        this.generator = generator;
        this.build_folder = build_folder;
        this.source_folder = source_folder;
    }

    private readonly Generator generator;
    private readonly string build_folder;
    private readonly string source_folder;
    private readonly List<Argument> arguments = new();

    // a cmake argument
    private class Argument
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
            AnsiConsole.WriteLine($"Setting CMake argument for config: {argument}");
            command.AddArgument(argument);
        }

        command.AddArgument(source_folder);
        command.AddArgument("-G");
        command.AddArgument(generator.Name);
        if (generator.Arch != null)
        {
            command.AddArgument("-A");
            command.AddArgument(generator.Arch);
        }

        Core.VerifyDirectoryExists(printer, build_folder);
        command.WorkingDirectory = build_folder;

        if (Core.IsWindows())
        {
            if (nop)
            {
                AnsiConsole.WriteLine($"Configuring cmake: {command}");
            }
            else
            {
                command.RunAndPrintOutput(printer);
            }
        }
        else
        {
            AnsiConsole.WriteLine($"Configuring cmake: {command}");
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

        if (install == CMake.Install.Yes)
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

        Core.VerifyDirectoryExists(printer, build_folder);
        command.WorkingDirectory = build_folder;

        if (Core.IsWindows())
        {
            command.RunAndPrintOutput(printer);
        }
        else
        {
            AnsiConsole.WriteLine($"Found path to cmake in path ({command}) but it didn't exist");
        }
    }

    // build cmake project
    public void Build(Printer printer, Config config)
    {
        RunBuildCommand(printer, CMake.Install.No, config);
    }

    // install cmake project
    public void Install(Printer printer, Config config)
    {
        RunBuildCommand(printer, CMake.Install.Yes, config);
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

    public static IEnumerable<Trace> TraceDirectory(string cmake_executable, string dir)
    {
        List<Trace> lines = new();
        List<string> error = new();

        var stderr = new List<string>();
        var ret = new ProcessBuilder(cmake_executable, "--trace-expand", "--trace-format=json-v1", "-S", Environment.CurrentDirectory, "-B", dir)
            .InDirectory(dir)
            .RunWithCallback(null, on_line, err => { on_line(err); stderr.Add(err); }, (err, ex) => { error.Add(err); error.Add(ex.Message);})
            .ExitCode
            ;

        if (ret == 0)
        {
            return lines;
        }

        var stderr_message = string.Join(Environment.NewLine, stderr).Trim();
        var space = string.IsNullOrEmpty(stderr_message) ? string.Empty : ": ";
        var error_message = string.Join(Environment.NewLine, error);
        throw new TraceError($"{stderr_message}{space}{error.Count} -> {error_message}");

        void on_line(string src)
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
    }

    
    public IEnumerable<string> ListFilesInCmakeLibrary()
    {
        return ListFilesInArgs("STATIC");
    }

    public IEnumerable<string> ListFilesInCmakeExecutable()
    {
        return ListFilesInArgs("WIN32", "MACOSX_BUNDLE");
    }

    private IEnumerable<string> ListFilesInArgs(params string[] arguments_to_ignore)
    {
        var folder = new FileInfo(File!).Directory?.FullName!;

        return Args
                .Skip(1) // name of library/app
                .SkipWhile(arguments_to_ignore.Contains)
                .SelectMany(a => a.Split(';'))
                .Select(f => new FileInfo(Path.Join(folder, f)).FullName)
            ;
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

        public TraceError(string? message, Exception? inner_exception) : base(message, inner_exception)
        {
        }

        protected TraceError(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }


}

