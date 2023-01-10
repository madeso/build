using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace Workbench.CMake;


static class CmakeTools
{
    public static Found find_cmake_in_registry(Printer printer)
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


    public static Found find_cmake_in_path(Printer printer)
    {
        var path_source = "path";
        var path = Which.Find("cmake");
        if(path == null)
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


    public static IEnumerable<Found> list_all(Printer printer)
    {
        yield return find_cmake_in_registry(printer);
        yield return find_cmake_in_path(printer);
    }


    public static string? find_cmake_executable(Printer printer)
    {
        return Found.first_value_or_none(list_all(printer));
    }
}

// a cmake argument
public class Argument
{
    public Argument(string name, string value)
    {
        this.name = name;
        this.value = value;
    }

    public Argument(string name, string value, string typename) : this(name, value)
    {
        this.typename = typename;
    }

    // format for commandline
    public string format_cmake_argument()
    {
        if (typename == null)
        {
            return $"-D{this.name}={this.value}";
        }
        else
        {
            return $"-D{this.name}:{typename}={this.value}";
        }
    }

    public string name { get; }
    public string value { get; }
    public string? typename { get; }
}


// cmake generator
public class Generator
{
    public Generator(string generator, string? arch = null)
    {
        this.generator = generator;
        this.arch = arch;
    }

    public string generator { get; }
    public string? arch { get; }
}

// utility to call cmake commands on a project
public class CMake
{
    public CMake(string build_folder, string source_folder, Generator generator)
    {
        this.generator = generator;
        this.build_folder = build_folder;
        this.source_folder = source_folder;
    }

    Generator generator { get; }
    string build_folder { get; }
    string source_folder { get; }
    List<Argument> arguments { get; } = new List<Argument>();


    // add argument with a explicit type set
    void add_argument_with_type(string name, string value, string typename)
    {
        this.arguments.Add(new Argument(name, value, typename));
    }

    // add argument
    public void add_argument(string name, string value)
    {
        this.arguments.Add(new Argument(name, value));
    }

    // set the install folder
    public void set_install_folder(string folder)
    {
        this.add_argument_with_type("CMAKE_INSTALL_PREFIX", folder, "PATH");
    }

    // set cmake to make static (not shared) library
    public void make_static_library()
    {
        this.add_argument("BUILD_SHARED_LIBS", "0");
    }

    // run cmake configure step
    public void config(Printer printer) { this.config_with_print(printer, false); }
    public void config_with_print(Printer printer, bool only_print)
    {
        var cmake = CmakeTools.find_cmake_executable(printer);
        if(cmake == null)
        {
            printer.error("CMake executable not found");
            return;
        }
        
        var command = new Command(cmake);
        foreach(var arg in this.arguments)
        {
            var argument = arg.format_cmake_argument();
            printer.info($"Setting CMake argument for config: {argument}");
            command.arg(argument);
        }

        command.arg(this.source_folder);
        command.arg("-G");
        command.arg(this.generator.generator);
        if(generator.arch != null)
        {
            command.arg("-A");
            command.arg(generator.arch);
        }
        
        Core.verify_dir_exist(printer, this.build_folder);
        command.current_dir(this.build_folder);
        
        if(Core.is_windows())
        {
            if(only_print)
            {
                printer.info($"Configuring cmake: {command}");
            }
            else
            {
                command.check_call(printer);
            }
        }
        else
        {
            printer.info($"Configuring cmake: {command}");
        }
    }

    // run cmake build step
    void build_cmd(Printer printer, bool install)
    {
        var cmake = CmakeTools.find_cmake_executable(printer);
        if (cmake == null)
        {
            printer.error("CMake executable not found");
            return;
        }

        var command = new Command(cmake);
        command.arg("--build");
        command.arg(".");

        if(install)
        {
            command.arg("--target");
            command.arg("install");
        }
        command.arg("--config");
        command.arg("Release");

        Core.verify_dir_exist(printer, this.build_folder);
        command.current_dir(this.build_folder);

        if(Core.is_windows())
        {
            command.check_call(printer);
        }
        else
        {
            printer.info($"Found path to cmake in path ({command}) but it didn't exist");
        }
    }

    // build cmake project
    public void build(Printer printer)
    {
        this.build_cmd(printer, false);
    }

    // install cmake project
    public void install(Printer printer)
    {
        this.build_cmd(printer, true);
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

        int ret = Run.GetOutput(Run.CreateProcessInFolder("cmake", "--trace-format=json-v1", dir), m => on_line(m), e => on_line(e));

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

