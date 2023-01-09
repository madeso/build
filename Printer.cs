using Spectre.Console;

class Printer
{
    int error_count = 0;
    readonly List<string> errors = new();

    // print a "pretty" header to the terminal
    public void header(string project_name) { this.header_with_custom_char(project_name, "-"); }
    void header_with_custom_char(string project_name, string header_character)
    {
        var header_size = 65;
        var header_spacing = 1;
        var header_start = 3;

        var spacing_string = " ".Repeat(header_spacing);
        var header_string = header_character.Repeat(header_size);

        var project = $"{spacing_string}{project_name}{spacing_string}";
        var start = header_character.Repeat(header_start);

        var left = header_size - (project.Length + header_start);
        var right =
            left > 1 ? header_character.Repeat(left)
            : ""
            ;

        AnsiConsole.MarkupLine($"{header_string}");
        AnsiConsole.MarkupLine($"{start}{project}{right}");
        AnsiConsole.MarkupLine($"{header_string}");
    }

    public void info(string text)
    {
        AnsiConsole.MarkupLine($"{text}");
    }

    public void line()
    {
        AnsiConsole.MarkupLine("-------------------------------------------------------------");
    }

    public void error(string text)
    {
        this.error_count += 1;
        this.errors.Add(text);
        AnsiConsole.MarkupLine($"ERROR: {text}");
    }

    public void warning(string text)
    {
        AnsiConsole.MarkupLine($"WARNING: {text}");
    }

    // print the contents of a single file
    public void cat(string path)
    {
        if (File.Exists(path))
        {
            AnsiConsole.MarkupLine($"{path}>");
            foreach (var line in File.ReadAllLines(path))
            {
                AnsiConsole.MarkupLine($"---->{line}");
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"Failed to open '{path}'");
        }
    }

    // print files and folder recursivly
    public void ls(string root) { this.ls_recursive(root, ""); }
    private void ls_recursive(string root, string start)
    {
        var ident = " ".Repeat(4);

        var paths = new DirectoryInfo(root);
        foreach (var file_path in paths.EnumerateDirectories())
        {
            AnsiConsole.MarkupLine("{}{}/", start, file_path.Name);
            this.ls_recursive(file_path.FullName, $"{start}{ident}");
        }

        foreach (var file_path in paths.EnumerateFiles())
        {
            AnsiConsole.MarkupLine($"{start}{file_path.Name}");
        }
    }

    public void exit_with_code()
    {
        if (error_count > 0)
        {
            AnsiConsole.MarkupLine($"Errors detected: ({error_count})");
        }

        foreach (var error in errors)
        {
            AnsiConsole.MarkupLine($"{error}");
        }
    }
}

