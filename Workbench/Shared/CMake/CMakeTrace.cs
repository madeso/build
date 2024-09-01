using System.Collections.Immutable;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Workbench.Shared.Extensions;

namespace Workbench.Shared.CMake
{
    public record FileInCmake(string Name, Fil File);

    public record CMakeTrace(Fil? File, int Line, string Cmd, ImmutableArray<string> Args)
    {
        private class CmakeOutputTrace
        {
            [JsonPropertyName("file")]
            public string? File { set; get; } = string.Empty;

            [JsonPropertyName("line")]
            public int Line { set; get; }

            [JsonPropertyName("cmd")]
            public string Cmd { set; get; } = string.Empty;

            [JsonPropertyName("args")]
            public string[] Args { set; get; } = Array.Empty<string>();
        }

        public static async Task<List<CMakeTrace>> TraceDirectoryAsync(Fil cmake_executable, Dir dir)
        {
            List<CMakeTrace> lines = new();
            List<string> error = new();

            var stderr = new List<string>();
            var ret = (await new ProcessBuilder(cmake_executable, "--trace-expand", "--trace-format=json-v1", "-S",
                            Dir.CurrentDirectory.Path, "-B", dir.Path)
                    .InDirectory(dir)
                    .RunWithCallbackAsync(null, on_line, err => { on_line(err); stderr.Add(err); },
                        (err, ex) => { error.Add(err); error.Add(ex.Message); })
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
                    var parsed = JsonSerializer.Deserialize<CmakeOutputTrace>(src);
                    if (parsed is { File: not null })
                    {
                        // file != null ignores the version json object
                        var traced_file = string.IsNullOrEmpty(parsed.File) ? null : new Fil(parsed.File);
                        lines.Add(new CMakeTrace(traced_file, parsed.Line, parsed.Cmd, parsed.Args.ToImmutableArray()));
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


        public IEnumerable<FileInCmake> ListFilesInLibraryOrExecutable()
        {
            return ListFilesInArgs("STATIC");
        }

        public IEnumerable<FileInCmake> ListFilesInCmakeExecutable()
        {
            return ListFilesInArgs("WIN32", "MACOSX_BUNDLE");
        }

        private IEnumerable<FileInCmake> ListFilesInArgs(params string[] arguments_to_ignore)
        {
            var folder = File?.Directory;

            if (folder == null)
                return Enumerable.Empty<FileInCmake>();

            return Args
                    .Skip(1) // name of library/app
                    .SkipWhile(arguments_to_ignore.Contains)
                    .SelectMany(a => a.Split(';'))
                    .Select(f => new FileInCmake(f, folder.GetFile(f)))
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

            [Obsolete("Obsolete")]
            protected TraceError(SerializationInfo info, StreamingContext context) : base(info, context)
            {
            }
        }


    }
}