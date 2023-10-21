namespace Workbench.Shared;


public static class Git
{
    public enum GitStatus
    {
        Unknown, Modified
    }

    public record GitStatusEntry(GitStatus Status, string Path);

    public static async IAsyncEnumerable<GitStatusEntry> StatusAsync(string folder)
    {
        var output = (await new ProcessBuilder("git", "status", "--porcelain=v1")
            .InDirectory(folder)
            .RunAndGetOutputAsync())
            .RequireSuccess()
            ;
        foreach (var item in output.Select(x => x.Line))
        {
            if (string.IsNullOrWhiteSpace(item)) { continue; }

            var line = item.Trim().Split(' ', 2, StringSplitOptions.TrimEntries);
            var type = line[0];

            var path = Path.Join(folder, to_path(line[1]));
            switch (type)
            {
                case "??": yield return new(GitStatus.Unknown, path); break;
                case "M": yield return new(GitStatus.Modified, path); break;
                default: throw new Exception($"Unhandled type <{type}> for line <{item}>");
            }

            continue;

            static string to_path(string v) => v.Trim().Replace("\"", "").Replace('/', '\\');
        }
    }

    public record Author(string Name, string Mail, DateTime Time);

    public record BlameLine
        (
            string Hash, int OriginalLineNumber, int FinalLineNumber, string Line,
            Author Author, Author Committer, string Summary, string Filename
        );

    public static async IAsyncEnumerable<BlameLine> BlameAsync(FileInfo file)
    {
        var output = (await new ProcessBuilder("git", "blame", "--porcelain", file.Name)
            .InDirectory(file.DirectoryName!)
            .RunAndGetOutputAsync())
            .RequireSuccess()
            ;
        var hash = string.Empty;
        var original_line_number = 0;
        var final_line_number = 0;
        var args = new Dictionary<string, string>();

        foreach (var line in output.Select(x => x.Line))
        {
            if (line[0] == '\t')
            {
                yield return new BlameLine(hash, original_line_number, final_line_number, line[1..],
                    parse_author("author"), parse_author("commiter"), get_arg("summary", ""), get_arg("filename", ""));
            }
            else
            {
                var cmd = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                var is_hash = cmd[0].Length == 40; // todo(Gustav): improve hash detection
                if (is_hash)
                {
                    var lines = cmd[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    hash = cmd[0];
                    original_line_number = int.Parse(lines[0]);
                    final_line_number = int.Parse(lines[1]);
                }
                else
                {
                    args[cmd[0]] = cmd[1];
                }
            }
        }

        yield break;

        Author parse_author(string prefix)
        {
            var name = get_arg(prefix, "");
            string mail = get_arg($"{prefix}-mail", "");
            string time_string = get_arg($"{prefix}-time", "0");
            var seconds = int.Parse(time_string);
            var time = DateTime.UnixEpoch.AddSeconds(seconds).ToLocalTime();
            return new Author(name, mail, time);
        }

        string get_arg(string name, string def)
        {
            if (args.TryGetValue(name, out var ret))
            {
                return ret;
            }
            else
            {
                return def;
            }
        }
    }

    public record LogLine
        (
            string Hash,
            string ParentHash,
            string AuthorName,
            string AuthorEmail,
            DateTime AuthorDate,
            string CommitterName,
            string CommitterEmail,
            DateTime CommitterDate,
            string Subject
        );

    public static async IAsyncEnumerable<LogLine> LogAsync(string folder)
    {
        const char SEPARATOR = ';';
        var log_format = string.Join(SEPARATOR, "%h", "%p", "%an", "%ae", "%aI", "%cn", "%ce", "%cI", "%s");
        var sep_count = log_format.Count(c => c == SEPARATOR);
        var output = (await new ProcessBuilder("git", "log", $"--format=format:{log_format}")
                .InDirectory(folder)
                .RunAndGetOutputAsync())
                .RequireSuccess()
            ;
        foreach (var line in output.Select(x => x.Line))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var options = line.Split(SEPARATOR, sep_count + 1, StringSplitOptions.TrimEntries);

            yield return new LogLine(options[0], options[1],
                options[2], options[3], DateTime.Parse(options[4]),
                options[5], options[6], DateTime.Parse(options[7]),
                options[8]);
        }
    }
}
