namespace Workbench.Shared;

using Spectre.Console;
using System;

public enum MessageType
{
    Error, Warning, Info
}

public record FileLine(Fil File, int? Line);


public interface Log
{
    void Error(FileLine? file, string message, string? code = null);
    void Error(string message);

    void Warning(FileLine? file, string message, string? code = null);
    void Warning(string message);

    void Info(FileLine? file, string message, string? code = null);
    void Info(string message);

    public void Print(MessageType message_type, FileLine? file, string message, string code)
    {
        switch (message_type)
        {
            case MessageType.Error: Error(file, message, code); break;
            case MessageType.Warning: Warning(file, message, code); break;
            case MessageType.Info: Info(file, message, code); break;
        }
    }

    public static string WithCode(MessageType type, string? code)
        => string.IsNullOrEmpty(code) ? type.ToString().ToUpper() : $"{type.ToString().ToLower()} {code}";

    public static string ToFileString(FileLine? file)
        => file == null
            ? "missing location"
            : $"{file.File.Path}({file.Line ?? -1})";

    void Raw(string message);
}

public class LogToConsole : Log
{
    // todo(Gustav): merge all functions into a few powerful versions together with output options on the log

    internal int error_count = 0;

    public void Error(FileLine? file, string message, string? code = null)
    {
        // todo(Gustav): require code and make error format a option
        AddError($"{Log.ToFileString(file)}: {Log.WithCode(MessageType.Error, code)}: {message}");
    }

    public void Error(string message)
    {
        AddError($"ERROR: {message}");
    }

    public void Warning(FileLine? file, string message, string? code = null)
    {
        Warning($"{Log.ToFileString(file)}: {Log.WithCode(MessageType.Warning, code)}: {message}");
    }

    public void Warning(string message)
    {
        AnsiConsole.MarkupLineInterpolated($"WARNING: {message}");
    }

    public void Info(FileLine? file, string message, string? code = null)
    {
        AnsiConsole.MarkupLineInterpolated($"[blue]{Log.ToFileString(file)}[/]: {Log.WithCode(MessageType.Info, code)}: {message}");
    }

    public void Info(string message)
    {
        if (is_connected_to_console())
        {
            AnsiConsole.WriteLine(message);
        }
        else
        {
            Console.WriteLine(message);
        }
    }

    public void Raw(string message)
    {
        Console.WriteLine(message);
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
    }

    private static bool is_connected_to_console()
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
