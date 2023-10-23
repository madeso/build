using Spectre.Console;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Workbench.Shared;
using Workbench.Shared.Extensions;

namespace Workbench.Commands.Todo;

internal record TodoInFile(FileInfo File, int Line, string Todo);

internal partial class TodoComments
{
    internal static async Task<ImmutableArray<TodoInFile>> FindTodosInFileAsync(string file)
    {
        var lines = await File.ReadAllLinesAsync(file);
        return lines
                    .Select((value, i) => (comment: ExtractTodoComment(value), lineNumber: i+1))
                    .Where(x => x.comment != null)
                    .Select(x => new TodoInFile(new FileInfo(file), x.lineNumber, x.comment!))
                    .ToImmutableArray()
        ;
    }

    public static ImmutableArray<string> ListFiles(DirectoryInfo root)
    {
        var build_folder = root.GetDir("build");
        
        var files = FileUtil.IterateFiles(root, false, true)
                .Where(f => FileUtil.ClassifySource(f) != Language.Unknown)
                .Select(f => f.FullName)
                .Where(x => build_folder.HasFile(x) == false)
            ;
        return files.ToImmutableArray();
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
