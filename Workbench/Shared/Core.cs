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
    public static void VerifyDirectoryExists(Log print, Dir dir)
    {
        if (dir.Exists)
        {
            AnsiConsole.WriteLine($"Dir exist, not creating {dir}");
        }
        else
        {
            AnsiConsole.WriteLine($"Not a directory, creating {dir}");
            try
            {
                dir.CreateDir();
            }
            catch (Exception x)
            {
                print.Error($"Failed to create directory {dir}: {x.Message}");
            }
        }
    }

    internal static string[]? ReadFileToLines(Fil filename)
    {
        if (filename.Exists)
        {
            return filename.ReadAllLines().ToArray();
        }
        else return null;
    }


    /// download file if not already downloaded 
    public static void DownloadFileIfMissing(Log print, string url, Fil dest)
    {
        if (dest.Exists)
        {
            AnsiConsole.WriteLine($"Already downloaded {dest}");
        }
        else
        {
            AnsiConsole.WriteLine($"Downloading {dest}");
            download_file(url, dest);
        }

        static void download_file(string url, Fil dest)
        {
            using var client = new HttpClient();
            using var s = client.GetStreamAsync(url);
            using var fs = new FileStream(dest.Path, FileMode.OpenOrCreate);
            s.Result.CopyTo(fs);
        }
    }

    /// moves all file from one directory to another
    public static void MoveFiles(Log print, Dir from, Dir to)
    {
        if (from.Exists == false)
        {
            print.Error($"Missing src {from} when moving to {to}");
            return;
        }

        VerifyDirectoryExists(print, to);
        move_files_recursively(from, to);

        static void move_files_recursively(Dir from, Dir to)
        {
            var paths = from;

            foreach (var file in paths.EnumerateFiles())
            {
                var src = from.GetFile(file.Name);
                var dst = to.GetFile(file.Name);
                File.Move(src.Path, dst.Path);
            }

            foreach (var dir in paths.EnumerateDirectories())
            {
                var src = from.GetDir(dir.Name);
                var dst = to.GetDir(dir.Name);
                Directory.CreateDirectory(dst.Path);
                move_files_recursively(src, dst);
                Directory.Delete(src.Path, false);
            }
        }
    }

    /// extract a zip file to folder
    public static void ExtractZip(Fil zip, Dir to)
    {
        ZipFile.ExtractToDirectory(zip.Path, to.Path);
    }

    public static string FormatNumber(int num)
    {
        var r = num.ToString("n0", CultureInfo.CurrentCulture);
        return r;
    }
}