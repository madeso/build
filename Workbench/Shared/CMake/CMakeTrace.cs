using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Workbench.Shared.CMake
{
    public class CMakeTrace
    {
        [JsonPropertyName("file")]
        public string? File { set; get; } = string.Empty;

        [JsonPropertyName("line")]
        public int Line { set; get; }

        [JsonPropertyName("cmd")]
        public string Cmd { set; get; } = string.Empty;

        [JsonPropertyName("args")]
        public string[] Args { set; get; } = Array.Empty<string>();

        public static async Task<IEnumerable<CMakeTrace>> TraceDirectoryAsync(string cmake_executable, string dir)
        {
            List<CMakeTrace> lines = new();
            List<string> error = new();

            var stderr = new List<string>();
            var ret = (await new ProcessBuilder(cmake_executable, "--trace-expand", "--trace-format=json-v1", "-S", Environment.CurrentDirectory, "-B", dir)
                    .InDirectory(dir)
                    .RunWithCallbackAsync(null, on_line, err => { on_line(err); stderr.Add(err); }, (err, ex) => { error.Add(err); error.Add(ex.Message); })
                    )
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
                    var parsed = JsonSerializer.Deserialize<CMakeTrace>(src);
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


        public IEnumerable<string> ListFilesInLibraryOrExecutable()
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
}