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
}
