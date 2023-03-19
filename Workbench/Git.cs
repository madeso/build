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
        foreach (var item in output)
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
            string Hash, int OriginalLineNumer, int FinalLineNumber, string Line,
            Author Author, Author Committer, string Summary, string Filename
        );

    public static IEnumerable<BlameLine> Blame(FileInfo file)
    {
        var output = new ProcessBuilder("git", "blame", "--porcelain", file.Name)
            .InDirectory(file.DirectoryName!)
            .RunAndGetOutput()
            .RequireSuccess()
            ;
        string hash = string.Empty;
        int originalLineNumber = 0;
        int finalLineNumber = 0;
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

        foreach (var line in output)
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
}
