namespace Workbench.Shared;

using Spectre.Console;
using System;

public enum MessageType
{
    Error, Warning
}

public record FileLine(string File, int? Line);
// public record ErrorClass(string Name, int Code);

public class Log
{

    // todo(Gustav): merge all functions into a few powerful versions together with output options on the log

    private int error_count = 0;

    internal void PrintError(FileLine? file, string message)
    {
        Error(file, $"{ToFileString(file)}: error CHK3030: {message}");
    }

    private static void PrintWarning(FileLine? file, string message)
    {
        Warning($"{ToFileString(file)}: warning CHK3030: {message}");
    }

    internal static void WriteInformation(FileLine? file, string message)
    {
        AnsiConsole.MarkupLineInterpolated($"[blue]{ToFileString(file)}[/]: {message}");
    }

    internal void Print(MessageType message_type, FileLine? file, string message)
    {
        switch (message_type)
        {
            case MessageType.Error: PrintError(file, message); break;
            case MessageType.Warning: PrintWarning(file, message); break;
        }
    }

    public void Error(string text)
    {
        AddError($"ERROR: {text}");
    }

    internal void Error(FileLine? file, string error)
    {
        AddError($"{ToFileString(file)}: ERROR: {error}");
    }

    private static string ToFileString(FileLine? file)
        => file == null
            ? "missing location"
            : $"{file.File}({file.Line ?? -1})";

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

        return;

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

    public static int PrintErrorsAtExit(Func<Log, int> callback)
    {
        var printer = new Log();
        var ret = callback(printer);
        if (printer.error_count > 0)
        {
            AnsiConsole.MarkupLineInterpolated($"Errors detected: ({printer.error_count})");
        }

        return ret;
    }
}

