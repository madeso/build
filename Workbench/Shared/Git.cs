using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Text;
using System.Text.RegularExpressions;

namespace Workbench.Shared;


public static class Git
{
    public enum GitStatus
    {
        Unknown, Modified,
        Added
    }

    // path is either to a file or directory
    public record GitStatusEntry(GitStatus Status, FileOrDir Path);

    public static async IAsyncEnumerable<GitStatusEntry> StatusAsync(Executor exec, Dir cwd, Fil git_path, Dir folder)
    {
        var output = (await new ProcessBuilder(git_path, "status", "--porcelain=v1")
            .InDirectory(folder)
            .RunAndGetOutputAsync(exec, cwd))
            .RequireSuccess()
            ;
        foreach (var item in output.Select(x => x.Line))
        {
            if (string.IsNullOrWhiteSpace(item)) { continue; }

            var line = item.Trim().Split(' ', 2, StringSplitOptions.TrimEntries);
            var type = line[0];

            var relative = to_path(line[1]);
            var absolute = Path.Join(folder.Path, relative);
            var path = FileOrDir.FromExistingOrNull(absolute);
            Debug.Assert(path != null);
            switch (type)
            {
                case "??": yield return new(GitStatus.Unknown, path); break;
                case "M": yield return new(GitStatus.Modified, path); break;
                case "A": yield return new(GitStatus.Added, path); break;
                default: throw new Exception($"Unhandled type <{type}> for line <{item}>");
            }

            continue;

            static string to_path(string v) => Unesacpe(v.Trim()).Replace("\"", "").Replace('/', '\\');
        }
    }

    private static string Unesacpe(string s)
    {
        // todo(Gustav): add more escape characters?
        var r = new Regex(@"\\([0-9]+)\\([0-9]+)");
        return r.Replace(s, m =>
        {
            var first_byte = Convert.ToInt32(m.Groups[1].Value, 8);
            var second_byte = Convert.ToInt32(m.Groups[2].Value, 8);
            var bytes = new[] { (byte)first_byte, (byte)second_byte };
            var r = Encoding.UTF8.GetString(bytes);
            return r;
        });
    }

    public record Author(string Name, string Mail, DateTime Time);

    public record BlameLine
        (
            string Hash, int OriginalLineNumber, int FinalLineNumber, string Line,
            Author Author, Author Committer, string Summary, string Filename
        );

    public static async IAsyncEnumerable<BlameLine> BlameAsync(Executor exec, Dir cwd, Fil git_path, Fil file)
    {
        var result = (await new ProcessBuilder(git_path, "blame", "--porcelain", file.Name)
            .InDirectory(file.Directory!)
            .RunAndGetOutputAsync(exec, cwd));
        if (result.ExitCode == 128)
        {
            // hope this means that the file isn't known to git...
            yield break;
        }

        var output = result
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
                if(cmd.Length <= 1) {continue;}

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
            var mail = get_arg($"{prefix}-mail", "");
            var time_string = get_arg($"{prefix}-time", "0");
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

    public static async IAsyncEnumerable<LogLine> LogAsync(Executor exec, Dir cwd, Fil git_path, Dir folder)
    {
        const char SEPARATOR = ';';
        var log_format = string.Join(SEPARATOR, "%h", "%p", "%an", "%ae", "%aI", "%cn", "%ce", "%cI", "%s");
        var sep_count = log_format.Count(c => c == SEPARATOR);
        var output = (await new ProcessBuilder(git_path, "log", $"--format=format:{log_format}")
                .InDirectory(folder)
                .RunAndGetOutputAsync(exec, cwd))
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

    public enum LineMod
    {
        Modified, Added,
        Deleted
    }
    public record FileLine(LineMod Modification, Fil File);

    public static async Task<ImmutableArray<FileLine>> FilesInCommitAsync(Executor exec, Dir cwd, Fil git_path, Dir folder, string commit)
    {
        // git diff-tree --no-commit-id --name-only bd61ad98 -r
        var output = (await new ProcessBuilder(git_path, "diff-tree", "--no-commit-id", "--name-status", commit, "-r")
                .InDirectory(folder)
                .RunAndGetOutputAsync(exec, cwd))
            .RequireSuccess();

        return output
            .Select(s => s.Line.Split('\t', 2, StringSplitOptions.TrimEntries))
            .Select(sp => new FileLine(sp[0] switch
            {
                "M" => LineMod.Modified,
                "A" => LineMod.Added,
                "D" => LineMod.Deleted,
                _ => throw new ArgumentOutOfRangeException(nameof(sp), sp[0], null)
            }, new Fil(sp[1])))
            .ToImmutableArray();
    }
}
