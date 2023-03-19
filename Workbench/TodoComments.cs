using Spectre.Console;
using System.Text.RegularExpressions;
using Workbench.Utils;

namespace Workbench;

internal partial class TodoComments
{
    internal static int PrintAll(Printer print, DirectoryInfo root)
    {
        var cc = new ColCounter<string>();
        
        foreach(var todo in ListAllTodos(root))
        {
            var fileAndLine = Printer.ToFileString(todo.File.FullName, todo.Line);
            AnsiConsole.MarkupLineInterpolated($"[blue]{fileAndLine}[/]: {todo.Todo}");
            cc.AddOne(todo.File.FullName);
        }

        {
            var count = cc.TotalCount();
            var files = cc.Keys.Count();
            AnsiConsole.MarkupLineInterpolated($"Found [blue]{count}[/] todos in {files} files");
        }

        AnsiConsole.WriteLine("Top 5 files");
        foreach(var (file, count) in cc.MostCommon().Take(5))
        {
            AnsiConsole.MarkupLineInterpolated($"[blue]{file}[/] with {count} todos");
        }

        return 0;
    }

    record TodoInFile(FileInfo File, int Line, string Todo);

    private static IEnumerable<TodoInFile> ListAllTodos(DirectoryInfo root)
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
