namespace Workbench.Shared;

using Spectre.Console;
using System;

public enum MessageType
{
    Error, Warning
}

public record FileLine(Fil File, int? Line);


public interface Log
{
    void Error(FileLine? file, string message);
    void Error(string message);
    void Warning(string message);

    void Print(MessageType message_type, FileLine? file, string message, string code);

    void PrintError(FileLine? file, string message, string? code);
    void WriteInformation(FileLine? file, string message);
}

public class LogToConsole : Log
{

    // todo(Gustav): merge all functions into a few powerful versions together with output options on the log

    internal int error_count = 0;

    public void PrintError(FileLine? file, string message, string? code)
    {
        // todo(Gustav): require code and make error format a option
        AddError(code != null
            ? $"{ToFileString(file)}: error {code}: {message}"
            : $"{ToFileString(file)}: ERROR: {message}");
    }

    public void Error(FileLine? file, string message)
    {
        // todo(Gustav): inline this useless function
        PrintError(file, message, null);
    }

    private void PrintWarning(FileLine? file, string message, string code)
    {
        Warning($"{ToFileString(file)}: warning {code}: {message}");
    }

    public void Warning(string message)
    {
        AnsiConsole.MarkupLineInterpolated($"WARNING: {message}");
    }

    public void WriteInformation(FileLine? file, string message)
    {
        AnsiConsole.MarkupLineInterpolated($"[blue]{ToFileString(file)}[/]: {message}");
    }

    public void Print(MessageType message_type, FileLine? file, string message, string code)
    {
        switch (message_type)
        {
            case MessageType.Error: PrintError(file, message, code); break;
            case MessageType.Warning: PrintWarning(file, message, code); break;
        }
    }

    public void Error(string text)
    {
        AddError($"ERROR: {text}");
    }

    private static string ToFileString(FileLine? file)
        => file == null
            ? "missing location"
            : $"{file.File.Path}({file.Line ?? -1})";

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
}

public static class CliUtil
{
    public static int PrintErrorsAtExit(Func<Log, int> callback)
    {
        var printer = new LogToConsole();
        var ret = callback(printer);
        if (printer.error_count > 0)
        {
            AnsiConsole.MarkupLineInterpolated($"Errors detected: {printer.error_count}");
            AnsiConsole.MarkupLineInterpolated($"Exit code: {ret}");
            if(ret == 0)
            {
                // if there are errors, then never allow a ok
                return -1;
            }
        }

        return ret;
    }

    public static async Task<int> PrintErrorsAtExitAsync(Func<Log, Task<int>> callback)
    {
        var printer = new LogToConsole();
        var ret = await callback(printer);
        if (printer.error_count > 0)
        {
            AnsiConsole.MarkupLineInterpolated($"Errors detected: {printer.error_count}");
            AnsiConsole.MarkupLineInterpolated($"Exit code: {ret}");
            if(ret == 0)
            {
                // if there are errors, then never allow a ok
                return -1;
            }
        }

        return ret;
    }
}
