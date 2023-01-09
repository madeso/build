namespace Workbench;

public static class CommonExecute
{
    public static int WithPrinter(Func<Printer, int> callback)
    {
        var printer = new Printer();
        return callback(printer);
    }

    public static int WithLoadedBuildData(Func<Printer, BuildData, int> callback)
    {
        return WithPrinter(print =>
        {
            var data = BuildData.load(print);
            if(data == null)
            {
                print.error("Unable to load the data");
                return -1;
            }
            else
            {
                return callback(print, data.Value);
            }
        });
    }
}
