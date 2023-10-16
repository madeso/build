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
        var extensions = FileUtil.HeaderAndSourceFiles.Concat(extra_extensions).ToArray();

        var build_folder = root.GetDir("build");

        var files = FileUtil.ListFilesRecursively(root, extensions)
            .Where(x => build_folder.HasFile(x) == false)
            ;

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
        var match = todo_comment.Match(line);
        return match.Success
            ? match.Captures[0].Value
            : null
            ;
    }

    [GeneratedRegex("// todo(.*)$", RegexOptions.Compiled)]
    private static partial Regex GenerateTodoCommentRegex();
    private static readonly Regex todo_comment = GenerateTodoCommentRegex();
}
