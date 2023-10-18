using Spectre.Console;

namespace Workbench.Shared;

public static class Printer
{
    // print a "pretty" header to the terminal
    public static void Header(string header_text)
    {
        AnsiConsole.Write(new Rule());
        AnsiConsole.Write(new Rule($"[red]{header_text}[/]"));
        AnsiConsole.Write(new Rule());
    }

    public static void Line()
    {
        AnsiConsole.Write(new Rule());
    }
}