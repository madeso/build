namespace Workbench;

public static class CommonExecute
{
    public static int WithPrinter(Func<Printer, int> callback)
    {
        var printer = new Printer();
        var ret = callback(printer);
        printer.PrintErrorCount();
        return ret;
    }
}
