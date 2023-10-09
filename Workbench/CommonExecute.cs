using Workbench.Build;
using Workbench.CheckIncludes;

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

    public static int WithLoadedBuildData(Func<Printer, BuildData, int> callback)
    {
        return WithPrinter(print =>
        {
            var data = BuildData.LoadOrNull(print);
            if (data == null)
            {
                print.Error("Unable to load the data");
                return -1;
            }
            else
            {
                return callback(print, data.Value);
            }
        });
    }

    public static int WithLoadedIncludeData(Func<Printer, IncludeData, int> callback)
    {
        return WithPrinter(print =>
        {
            var data = IncludeData.LoadOrNull(print);
            if (data == null)
            {
                print.Error("Unable to load the data");
                return -1;
            }
            else
            {
                return callback(print, data.Value);
            }
        });
    }
}
