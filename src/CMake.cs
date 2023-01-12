using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace Workbench.CMake;


static class CmakeTools
{
    private static Found FindnRegistry(Printer printer)
    {
        var registry_source = "registry";

        var install_dir = Registry.hklm(@"SOFTWARE\Kitware\CMake", "InstallDir");
        if (install_dir == null) { return new Found(null, registry_source); }

        var path = Path.Join(install_dir, "bin", "cmake.exe");
        if (File.Exists(path) == false)
        {
            printer.error($"Found path to cmake in registry ({path}) but it didn't exist");
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
            printer.error($"Found path to cmake in path ({path}) but it didn't exist");
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
        return Found.first_value_or_none(ListAll(printer));
    }
}

// a cmake argument
public class Argument
{
    public Argument(string name, string value)
    {
        this.Name = name;
        this.Value = value;
    }

    public Argument(string name, string value, string typename) : this(name, value)
    {
        this.TypeName = typename;
    }

    // format for commandline
    public string FormatForCmakeArgument()
    {
        if (TypeName == null)
        {
            return $"-D{this.Name}={this.Value}";
        }
        else
        {
            return $"-D{this.Name}:{TypeName}={this.Value}";
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
        this.Name = name;
        this.Arch = arch;
    }

    public string Name { get; }
    public string? Arch { get; }
}

// utility to call cmake commands on a project
public class CMake
{
    public CMake(string build_folder, string source_folder, Generator generator)
    {
        this.generator = generator;
        this.buildFolder = build_folder;
        this.sourceFolder = source_folder;
    }

    private readonly Generator generator;
    private readonly string buildFolder;
    private readonly string sourceFolder;
    private readonly List<Argument> arguments = new List<Argument>();


    // add argument with a explicit type set
    private void AddArgumentWithType(string name, string value, string typename)
    {
        this.arguments.Add(new Argument(name, value, typename));
    }

    // add argument
    public void AddArgument(string name, string value)
    {
        this.arguments.Add(new Argument(name, value));
    }

    // set the install folder
    public void SetInstallFolder(string folder)
    {
        this.AddArgumentWithType("CMAKE_INSTALL_PREFIX", folder, "PATH");
    }

    // set cmake to make static (not shared) library
    public void MakeStaticLibrary()
    {
        this.AddArgument("BUILD_SHARED_LIBS", "0");
    }

    // run cmake configure step
    public void Configure(Printer printer, bool nop = false)
    {
        var cmake = CmakeTools.FindOrNull(printer);
        if (cmake == null)
        {
            printer.error("CMake executable not found");
            return;
        }

        var command = new Command(cmake);
        foreach (var arg in this.arguments)
        {
            var argument = arg.FormatForCmakeArgument();
            printer.Info($"Setting CMake argument for config: {argument}");
            command.AddArgument(argument);
        }

        command.AddArgument(this.sourceFolder);
        command.AddArgument("-G");
        command.AddArgument(this.generator.Name);
        if (generator.Arch != null)
        {
            command.AddArgument("-A");
            command.AddArgument(generator.Arch);
        }

        Core.VerifyDirectoryExists(printer, this.buildFolder);
        command.WorkingDirectory = this.buildFolder;

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
            printer.error("CMake executable not found");
            return;
        }

        var command = new Command(cmake);
        command.AddArgument("--build");
        command.AddArgument(".");

        if (install)
        {
            command.AddArgument("--target");
            command.AddArgument("install");
        }
        command.AddArgument("--config");
        command.AddArgument("Release");

        Core.VerifyDirectoryExists(printer, this.buildFolder);
        command.WorkingDirectory = this.buildFolder;

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
        this.RunBuildCommand(printer, false);
    }

    // install cmake project
    public void Install(Printer printer)
    {
        this.RunBuildCommand(printer, true);
    }
}

[JsonObject(MemberSerialization.OptIn)]
public class Trace
{
    public Trace(string file, int line, string cmd, string[] args)
    {
        this.File = file;
        this.Line = line;
        this.Cmd = cmd;
        this.Args = args;
    }

    [JsonProperty("file")]
    public string File { get; }

    [JsonProperty("line")]
    public int Line { get; }

    [JsonProperty("cmd")]
    public string Cmd { get; }

    [JsonProperty("args")]
    public string[] Args { get; }

    public static IEnumerable<Trace> TraceDirectory(string dir)
    {
        List<Trace> lines = new();
        List<string> error = new();

        void on_line(string src)
        {
            try
            {
                var parsed = JsonConvert.DeserializeObject<Trace>(src);
                if (parsed != null && parsed.File != null)
                {
                    // file != null ignores the version json object
                    lines.Add(parsed);
                    return;
                }
            }
            catch (Newtonsoft.Json.JsonReaderException)
            {
                // pass
            }

            error.Add(src);
        }

        var ret = new Command("cmake", "--trace-format=json-v1")
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

