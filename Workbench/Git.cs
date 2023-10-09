using System.Net.Http.Headers;
using System.Xml.Linq;
using Workbench.Doxygen.Compound;
using Workbench.Utils;

namespace Workbench;


public static class Git
{
    public enum GitStatus
    {
        Unknown, Modified
    }

    public record GitStatusEntry(GitStatus Status, string Path);

    public static IEnumerable<GitStatusEntry> Status(string folder)
    {
        var output = new ProcessBuilder("git", "status", "--porcelain=v1")
            .InDirectory(folder)
            .RunAndGetOutput()
            .RequireSuccess()
            ;
        foreach (var item in output.Select(x => x.Line))
        {
            if (string.IsNullOrWhiteSpace(item)) { continue; }

            var line = item.Trim().Split(' ', 2, StringSplitOptions.TrimEntries);
            var type = line[0];

            static string ToPath(string v) => v.Trim().Replace("\"", "").Replace('/', '\\');
            var path = Path.Join(folder, ToPath(line[1]));
            switch (type)
            {
                case "??": yield return new(GitStatus.Unknown, path); break;
                case "M": yield return new(GitStatus.Modified, path); break;
                default: throw new Exception($"Unhandled type <{type}> for line <{item}>");
            }
        }
    }

    public record Author(string Name, string Mail, DateTime Time);

    public record BlameLine
        (
            string Hash, int OriginalLineNumber, int FinalLineNumber, string Line,
            Author Author, Author Committer, string Summary, string Filename
        );

    public static IEnumerable<BlameLine> Blame(FileInfo file)
    {
        var output = new ProcessBuilder("git", "blame", "--porcelain", file.Name)
            .InDirectory(file.DirectoryName!)
            .RunAndGetOutput()
            .RequireSuccess()
            ;
        var hash = string.Empty;
        var originalLineNumber = 0;
        var finalLineNumber = 0;
        var args = new Dictionary<string, string>();
        
        string GetArg(string name, string def)
        {
            if(args!.TryGetValue(name, out var ret))
            {
                return ret;
            }
            else
            {
                return def;
            }
        }
        
        Author ParseAuthor(string prefix)
        {
            var name = GetArg(prefix, "");
            string mail = GetArg($"{prefix}-mail", "");
            string timeString = GetArg($"{prefix}-time", "0");
            var seconds = int.Parse(timeString);
            var time = DateTime.UnixEpoch.AddSeconds(seconds).ToLocalTime();
            return new Author(name, mail, time);
        }

        foreach (var line in output.Select(x => x.Line))
        {
            if(line[0] == '\t')
            {
                yield return new BlameLine(hash, originalLineNumber, finalLineNumber, line[1..],
                    ParseAuthor("author"), ParseAuthor("commiter"), GetArg("summary", ""), GetArg("filename", ""));
            }
            else
            {
                var cmd = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                var isHash = cmd[0].Length == 40; // todo(Gustav): improve hash detection
                if(isHash)
                {
                    var lines = cmd[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    hash = cmd[0];
                    originalLineNumber = int.Parse(lines[0]);
                    finalLineNumber = int.Parse(lines[1]);
                }
                else
                {
                    args[cmd[0]] = cmd[1];
                }
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

    public static IEnumerable<LogLine> Log(string folder)
    {
        const char separator = ';';
        var logFormat = string.Join(separator, "%h", "%p", "%an", "%ae", "%aI", "%cn", "%ce", "%cI", "%s");
        var sepCount = logFormat.Count(c => c == separator);
        var output = new ProcessBuilder("git", "log", $"--format=format:{logFormat}")
                .InDirectory(folder)
                .RunAndGetOutput()
                .RequireSuccess()
            ;
        foreach (var line in output.Select(x => x.Line))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var options = line.Split(separator, sepCount+1, StringSplitOptions.TrimEntries);

            yield return new LogLine(options[0], options[1],
                options[2], options[3], DateTime.Parse(options[4]),
                options[5], options[6], DateTime.Parse(options[7]),
                options[8]);
        }
    }
}
