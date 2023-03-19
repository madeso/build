using Spectre.Console;
using System.Text.RegularExpressions;
using Workbench.Utils;

namespace Workbench;

internal record TodoInFile(FileInfo File, int Line, string Todo);

internal partial class TodoComments
{
    internal static IEnumerable<TodoInFile> ListAllTodos(DirectoryInfo root)
    {
        var extra_extensions = new string[] { ".jsonc" };
        var extensions = FileUtil.HEADER_AND_SOURCE_FILES.Concat(extra_extensions).ToArray();
        var files = FileUtil.ListFilesRecursivly(root, extensions);

        return
            files.SelectMany(file =>
                File.ReadAllLines(file)
                    .Select((value, i) => (comment: ExtractTodoComment(value), lineNumber: i+1))
                    .Where(x => x.comment != null)
                    .Select(x => new TodoInFile(new FileInfo(file), x.lineNumber, x.comment!))
        );
    }

    private static string? ExtractTodoComment(string line)
    {
        var match = TodoComment.Match(line);
        if(match.Success)
        {
            return match.Captures[0].Value;
        }
        else
        {
            return null;
        }
    }

    [GeneratedRegex("// todo(.*)$", RegexOptions.Compiled)]
    private static partial Regex GenerateTodoCommentRegex();
    private static readonly Regex TodoComment = GenerateTodoCommentRegex();
}
