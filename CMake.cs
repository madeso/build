using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace Workbench.CMake
{

#if false
    fn find_cmake_in_registry(printer: &mut printer::Printer) -> found::Found
{
    let registry_source = "registry".to_string();

    match registry::hklm(r"SOFTWARE\Kitware\CMake", "InstallDir")
    {
        Err(_) => found::Found::new(None, registry_source),
        Ok(install_dir) =>
        {
            let bpath: PathBuf = [install_dir.as_str(), "bin", "cmake.exe"].iter().collect();
            let spath = bpath.as_path();
            let path = spath.to_str().unwrap();
            if spath.exists()
            {
                found::Found::new(Some(path.to_string()), registry_source)
            }
            else
            {
                printer.error(format!("Found path to cmake in registry ({}) but it didn't exist", path).as_str());
                found::Found::new(None, registry_source)
            }
        }
    }
}


fn find_cmake_in_path(printer: &mut printer::Printer) -> found::Found
{
    let path_source = "path".to_string();
    match which::which("cmake")
    {
        Err(_) => found::Found::new(None, path_source),
        Ok(bpath) =>
        {
            let spath = bpath.as_path();
            let path = spath.to_str().unwrap();
            if spath.exists()
            {
                found::Found::new(Some(path.to_string()), path_source)
            }
            else
            {
                printer.error(format!("Found path to cmake in path ({}) but it didn't exist", path).as_str());
                found::Found::new(None, path_source)
            }
        }
    }
}


pub fn list_all(printer: &mut printer::Printer) -> Vec::<found::Found>
{
    vec![find_cmake_in_registry(printer), find_cmake_in_path(printer)]
}


fn find_cmake_executable(printer: &mut printer::Printer) -> Option<String>
{
    let list = list_all(printer);
    found::first_value_or_none(&list)
}
#endif

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

        string generator { get; }
        string? arch { get; }
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
        void add_argument(string name, string value)
        {
            this.arguments.Add(new Argument(name, value));
        }

        // set the install folder
        void set_install_folder(string folder)
        {
            this.add_argument_with_type("CMAKE_INSTALL_PREFIX", folder, "PATH");
        }

        // set cmake to make static (not shared) library
        void make_static_library()
        {
            this.add_argument("BUILD_SHARED_LIBS", "0");
        }

#if false
        // run cmake configure step
        void config(Printer printer) { this.config_with_print(printer, false); }
        void config_with_print(Printer printer, bool only_print)
        {
            let found_cmake = find_cmake_executable(printer);
            let cmake = match found_cmake
            {
                Some(f) => {f},
                None =>
                {
                    printer.error("CMake executable not found");
                    return;
                }
            };
        
            let mut command = Command::new(cmake);
            for arg in &this.arguments
            {
                let argument = arg.format_cmake_argument();
                printer.info(format!("Setting CMake argument for config: {}", argument).as_str());
                command.arg(argument);
            }

            command.arg(this.source_folder.to_string_lossy().to_string());
            command.arg("-G");
            command.arg(this.generator.generator.as_str());
            match &this.generator.arch
            {
                Some(arch) =>
                {
                    command.arg("-A");
                    command.arg(arch);
                }
                None => {}
            }
        
            core::verify_dir_exist(printer, &this.build_folder);
            command.current_dir(this.build_folder.to_string_lossy().to_string());
        
            if core::is_windows()
            {
                if only_print
                {
                    printer.info(format!("Configuring cmake: {:?}", command).as_str());
                }
                else
                {
                    // core::flush();
                    cmd::check_call(printer, &mut command);
                }
            }
            else
            {
                printer.info(format!("Configuring cmake: {:?}", command).as_str());
            }
        }

        // run cmake build step
        void build_cmd(Printer printer, bool install)
        {
            let found_cmake = find_cmake_executable(printer);
            let cmake = match found_cmake
            {
                Some(f) => {f},
                None =>
                {
                    printer.error("CMake executable not found");
                    return;
                }
            };

            let mut command = Command::new(cmake);
            command.arg("--build");
            command.arg(".");

            if install
            {
                command.arg("--target");
                command.arg("install");
            }
            command.arg("--config");
            command.arg("Release");

            core::verify_dir_exist(printer, &this.build_folder);
            command.current_dir(this.build_folder.to_string_lossy().to_string());

            if core::is_windows()
            {
                // core::flush()
                cmd::check_call(printer, &mut command);
            }
            else
            {
                printer.info(format!("Calling build on cmake: {:?}", command).as_str());
            }
        }

        // build cmake project
        void build(Printer printer)
        {
            this.build_cmd(printer, false);
        }

        // install cmake project
        void install(Printer printer)
        {
            this.build_cmd(printer, true);
        }
#endif
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
                catch(Newtonsoft.Json.JsonReaderException)
                {
                    // pass
                }
                
                error.Add(src);
            }

            int ret = Run.GetOutput(Run.CreateProcessInFolder("cmake", "--trace-format=json-v1", dir), m => on_line(m), e => on_line(e));

            if (ret !=  0)
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
}
