namespace Workbench;

using Spectre.Console;

public class Printer
{
    private int error_count = 0;
    private readonly List<string> errors = new();

    // print a "pretty" header to the terminal
    public void Header(string headerText) { HeaderWithCustomChar(headerText, "-"); }

    private void HeaderWithCustomChar(string projectName, string headerCharacter)
    {
        var header_size = 65;
        var header_spacing = 1;
        var header_start = 3;

        var spacing_string = " ".Repeat(header_spacing);
        var header_string = headerCharacter.Repeat(header_size);

        var project = $"{spacing_string}{projectName}{spacing_string}";
        var start = headerCharacter.Repeat(header_start);

        var left = header_size - (project.Length + header_start);
        var right =
            left > 1 ? headerCharacter.Repeat(left)
            : ""
            ;

        AnsiConsole.MarkupLineInterpolated($"{header_string}");
        AnsiConsole.MarkupLineInterpolated($"{start}{project}{right}");
        AnsiConsole.MarkupLineInterpolated($"{header_string}");
    }

    public void Info(string text)
    {
        AnsiConsole.MarkupLineInterpolated($"{text}");
    }

    public void Line()
    {
        AnsiConsole.MarkupLine("-------------------------------------------------------------");
    }

    public void Error(string text)
    {
        error_count += 1;
        errors.Add(text);
        AnsiConsole.MarkupLineInterpolated($"[red]ERROR: {text}[/]");
    }

    public void Warning(string text)
    {
        AnsiConsole.MarkupLineInterpolated($"WARNING: {text}");
    }

    public void PrintContentsOfFile(string path)
    {
        if (File.Exists(path))
        {
            AnsiConsole.MarkupLineInterpolated($"{path}>");
            foreach (var line in File.ReadAllLines(path))
            {
                AnsiConsole.MarkupLineInterpolated($"---->{line}");
            }
        }
        else
        {
            AnsiConsole.MarkupLineInterpolated($"Failed to open '{path}'");
        }
    }

    // print files and folder recursivly
    public void PrintDirectoryStructure(string root) {
        PrinteRecursive(root, "");

        static void PrinteRecursive(string root, string start)
        {
            var ident = " ".Repeat(4);

            var paths = new DirectoryInfo(root);
            foreach (var file_path in paths.EnumerateDirectories())
            {
                AnsiConsole.MarkupLine("{}{}/", start, file_path.Name);
                PrinteRecursive(file_path.FullName, $"{start}{ident}");
            }

            foreach (var file_path in paths.EnumerateFiles())
            {
                AnsiConsole.MarkupLineInterpolated($"{start}{file_path.Name}");
            }
        }
    }
    

    public void PrintErrorCount()
    {
        if (error_count > 0)
        {
            AnsiConsole.MarkupLineInterpolated($"Errors detected: ({error_count})");
        }
    }
}

