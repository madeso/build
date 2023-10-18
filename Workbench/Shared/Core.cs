using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Spectre.Console;

namespace Workbench.Shared;

public static class Core
{
    public static bool IsWindows()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }

    // check if the script is running on 64bit or not
    public static bool Is64Bit()
    {
        return RuntimeInformation.ProcessArchitecture == Architecture.X64;
    }

    /// make sure directory exists 
    public static void VerifyDirectoryExists(Log print, string dir)
    {
        if (Directory.Exists(dir))
        {
            AnsiConsole.WriteLine($"Dir exist, not creating {dir}");
        }
        else
        {
            AnsiConsole.WriteLine($"Not a directory, creating {dir}");
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
    public static void DownloadFileIfMissing(Log print, string url, string dest)
    {
        if (File.Exists(dest))
        {
            AnsiConsole.WriteLine($"Already downloaded {dest}");
        }
        else
        {
            AnsiConsole.WriteLine($"Downloading {dest}");
            download_file(url, dest);
        }

        static void download_file(string url, string dest)
        {
            using var client = new HttpClient();
            using var s = client.GetStreamAsync(url);
            using var fs = new FileStream(dest, FileMode.OpenOrCreate);
            s.Result.CopyTo(fs);
        }
    }

    /// moves all file from one directory to another
    public static void MoveFiles(Log print, string from, string to)
    {
        if (Path.Exists(from) == false)
        {
            print.Error($"Missing src {from} when moving to {to}");
            return;
        }

        VerifyDirectoryExists(print, to);
        move_files_recursively(from, to);

        static void move_files_recursively(string from, string to)
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
                move_files_recursively(src, dst);
                Directory.Delete(src, false);
            }
        }
    }

    /// extract a zip file to folder
    public static void ExtractZip(string zip, string to)
    {
        ZipFile.ExtractToDirectory(zip, to);
    }

    public static string FormatNumber(int num)
    {
        var r = num.ToString("n0", CultureInfo.CurrentCulture);
        return r;
    }
}