namespace Workbench.Shared;

using Spectre.Console;
using System;
using Shared;

public class Printer
{
    private int error_count = 0;

    // print a "pretty" header to the terminal
    public static void Header(string header_text)
    {
        const int HEADER_SIZE = 65;
        const int HEADER_SPACING = 1;
        const int HEADER_START = 3;

        var spacing_string = " ".Repeat(HEADER_SPACING);
        var header_string = "-".Repeat(HEADER_SIZE);

        var project = $"{spacing_string}{header_text}{spacing_string}";
        var start = "-".Repeat(HEADER_START);

        var left = HEADER_SIZE - (project.Length + HEADER_START);
        var right =
                left > 1 ? "-".Repeat(left)
                    : ""
            ;

        AnsiConsole.MarkupLineInterpolated($"{header_string}");
        AnsiConsole.MarkupLineInterpolated($"{start}{project}{right}");
        AnsiConsole.MarkupLineInterpolated($"{header_string}");
    }

    public static void Line()
    {
        AnsiConsole.MarkupLine("-------------------------------------------------------------");
    }

    public void Error(string text)
    {
        AddError($"ERROR: {text}");
    }

    internal void Error(string file, string error)
    {
        AddError($"{file}: ERROR: {error}");
    }

    private void AddError(string message)
    {
        error_count += 1;
        if (is_connected_to_console())
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{message}[/]");
        }
        else
        {
            Console.WriteLine(message);
        }

        static bool is_connected_to_console()
        {
            try
            {
                return Console.WindowWidth > 0;
            }
            catch (IOException)
            {
                return false;
            }
        }
    }

    public static void Warning(string text)
    {
        AnsiConsole.MarkupLineInterpolated($"WARNING: {text}");
    }

    public void PrintErrorCount()
    {
        if (error_count > 0)
        {
            AnsiConsole.MarkupLineInterpolated($"Errors detected: ({error_count})");
        }
    }

    internal static string ToFileString(string file_name, int line)
    {
        return $"{file_name}({line})";
    }

    public static int PrintErrorsAtExit(Func<Printer, int> callback)
    {
        var printer = new Printer();
        var ret = callback(printer);
        printer.PrintErrorCount();
        return ret;
    }
}

