namespace Workbench;

using Spectre.Console;
using System;
using Workbench.Utils;

public class Printer
{
    private int _errorCount = 0;
    private readonly List<string> errors = new();

    // print a "pretty" header to the terminal
    public static void Header(string headerText) { HeaderWithCustomChar(headerText, "-"); }

    private static void HeaderWithCustomChar(string projectName, string headerCharacter)
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

    public static void Info(string text)
    {
        AnsiConsole.MarkupLineInterpolated($"{text}");
    }

    public static void Line()
    {
        AnsiConsole.MarkupLine("-------------------------------------------------------------");
    }

    public void Error(string text)
    {
        _errorCount += 1;
        errors.Add(text);
        AnsiConsole.MarkupLineInterpolated($"[red]ERROR: {text}[/]");
    }

    internal void Error(string file, string error)
    {
        var text = $"{file}: {error}";
        _errorCount += 1;
        errors.Add(text);
        var message = $"{file}: ERROR: {error}";
        if(IsConnectedToConsole())
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{message}[/]");
        }
        else
        {
            Console.WriteLine(message);
        }

        static bool IsConnectedToConsole()
        {
            try
            {
                return Console.WindowWidth > 0;
            }
            catch(IOException)
            {
                return false;
            }
        }
    }

    public static void Warning(string text)
    {
        AnsiConsole.MarkupLineInterpolated($"WARNING: {text}");
    }

    public static void PrintContentsOfFile(string path)
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
    public static void PrintDirectoryStructure(string root) {
        PrintRecursive(root, "");

        static void PrintRecursive(string root, string start)
        {
            var ident = " ".Repeat(4);

            var paths = new DirectoryInfo(root);
            foreach (var filePath in paths.EnumerateDirectories())
            {
                AnsiConsole.MarkupLineInterpolated($"{start}{filePath.Name}/");
                PrintRecursive(filePath.FullName, $"{start}{ident}");
            }

            foreach (var filePath in paths.EnumerateFiles())
            {
                AnsiConsole.MarkupLineInterpolated($"{start}{filePath.Name}");
            }
        }
    }
    

    public void PrintErrorCount()
    {
        if (_errorCount > 0)
        {
            AnsiConsole.MarkupLineInterpolated($"Errors detected: ({_errorCount})");
        }
    }

    internal void PrintStatus(ProcessExit pe)
    {
        var message = $"{pe.CommandLine} exited with {pe.ExitCode}";
        if(pe.ExitCode == 0)
        {
            Info(message);
        }
        else
        {
            Error(message);
        }
    }

    internal static string ToFileString(string fileName, int line)
    {
        return $"{fileName}({line})";
    }
}

