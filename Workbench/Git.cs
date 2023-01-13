namespace Workbench.Git;

public enum GitStatus
{
    Unknown, Modified
}

public class GitStatusEntry
{
    public GitStatusEntry(GitStatus status, string path)
    {
        Status = status;
        Path = path;
    }

    public GitStatus Status { get; }
    public string Path { get; }
}

public static class Git
{
    public static IEnumerable<GitStatusEntry> Status(string folder)
    {
        var output = new Command("git", "status", "--porcelain=v1")
            .InDirectory(folder)
            .RunAndGetOutput()
            .RequireSuccess()
            ;
        foreach (var item in output)
        {
            if (string.IsNullOrWhiteSpace(item)) { continue; }

            var line = item.Trim().Split(' ', 2, StringSplitOptions.TrimEntries);
            var type = line[0];
            var path = Path.Join(folder, ToPath(line[1]));
            switch (type)
            {
                case "??": yield return new(GitStatus.Unknown, path); break;
                case "M": yield return new(GitStatus.Modified, path); break;
                default: throw new Exception($"Unhandled type <{type}> for line <{item}>");
            }
        }
    }

    private static ReadOnlySpan<char> ToPath(string v)
    {
        return v.Trim().Replace("\"", "").Replace('/', '\\');
    }
}
