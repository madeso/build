using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace Workbench;

public static class Core
{
    public static IEnumerable<string> ListAllFiles(string rootDirectory)
    {
        var dirs = new Queue<string>();
        dirs.Enqueue(rootDirectory);

        while (dirs.Count > 0)
        {
            var dir = new DirectoryInfo(dirs.Dequeue());
            foreach (var di in dir.GetDirectories())
            {
                dirs.Enqueue(di.FullName);
            }
            foreach (var f in dir.GetFiles())
            {
                yield return f.FullName;
            }
        }
    }

    public static bool IsWindows()
    {
        return System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }

    // check if the script is running on 64bit or not
    public static bool Is64Bit()
    {
        return System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == Architecture.X64;
    }

    /// make sure directory exists 
    public static void VerifyDirectoryExists(Printer print, string dir)
    {
        if (Directory.Exists(dir))
        {
            print.Info($"Dir exist, not creating {dir}");
        }
        else
        {
            print.Info($"Not a directory, creating {dir}");
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch (Exception x)
            {
                print.Error($"Failed to create directory {dir}: {x.Message}");
            }
        }
    }

    internal static string[]? ReadFileToLines(string filename)
    {
        if (File.Exists(filename))
        {
            return File.ReadAllLines(filename);
        }
        else return null;
    }


    /// download file if not already downloaded 
    public static void DownloadFileIfMissing(Printer print, string url, string dest)
    {
        if (File.Exists(dest))
        {
            print.Info($"Already downloaded {dest}");
        }
        else
        {
            print.Info($"Downloading {dest}");
            DownloadFile(print, url, dest);
        }

        static void DownloadFile(Printer print, string url, string dest)
        {
            using var client = new HttpClient();
            using var s = client.GetStreamAsync(url);
            using var fs = new FileStream(dest, FileMode.OpenOrCreate);
            s.Result.CopyTo(fs);
        }
    }

    /// moves all file from one directory to another
    public static void MoveFiles(Printer print, string from, string to)
    {
        if (Path.Exists(from) == false)
        {
            print.Error($"Missing src {from} when moving to {to}");
            return;
        }

        VerifyDirectoryExists(print, to);
        MoveFilesRecursivly(from, to);

        static void MoveFilesRecursivly(string from, string to)
        {
            var paths = new DirectoryInfo(from);

            foreach (var file in paths.EnumerateFiles())
            {
                var src = Path.Join(from, file.Name);
                var dst = Path.Join(to, file.Name);
                File.Move(src, dst);
            }

            foreach (var dir in paths.EnumerateDirectories())
            {
                var src = Path.Join(from, dir.Name);
                var dst = Path.Join(to, dir.Name);
                Directory.CreateDirectory(dst);
                MoveFilesRecursivly(src, dst);
                Directory.Delete(src, false);
            }
        }
    }

    /// extract a zip file to folder
    public static void ExtractZip(Printer print, string zip, string to)
    {
        ZipFile.ExtractToDirectory(zip, to);
    }

    public static string FormatNumber(int num)
    {
        var r = num.ToString("n0", CultureInfo.CurrentCulture);
        return r;
    }
}

/// multi replace calls on a single text 
public class TextReplacer
{
    private readonly record struct SingleReplacement(string From, string To);

    private readonly List<SingleReplacement> replacements = new();

    // add a replacement command 
    public void Add(string old, string neww)
    {
        replacements.Add(new SingleReplacement(old, neww));
    }

    public string Replace(string inText)
    {
        var text = inText;

        foreach (var replacement in replacements)
        {
            text = text.Replace(replacement.From, replacement.To);
        }

        return text;
    }
}