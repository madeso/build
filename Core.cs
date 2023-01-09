using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;

namespace Workbench;

public static class Core
{
    public static IEnumerable<string> walk_files(string sdir)
    {
        var dirs = new Queue<string>();
        dirs.Enqueue(sdir);

        while(dirs.Count > 0)
        {
            var dir = new DirectoryInfo(dirs.Dequeue());
            foreach(var di in dir.GetDirectories())
            {
                dirs.Enqueue(di.FullName);
            }
            foreach(var f in dir.GetFiles())
            {
                yield return f.FullName;
            }
        }
    }

    public static bool is_windows()
    {
        return System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }

    // check if the script is running on 64bit or not
    public static bool is_64bit()
    {
        return System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == Architecture.X64;
    }

    /// make sure directory exists 
    public static void verify_dir_exist(Printer print, string dir)
    {
        if(Directory.Exists(dir))
        {
            print.info($"Dir exist, not creating {dir}");
        }
        else
        {
            print.info($"Not a directory, creating {dir}");
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch(Exception x)
            {
                print.error($"Failed to create directory {dir}: {x.Message}");
            }
        }
    }

    internal static string[]? read_file_to_lines(string filename)
    {
        if (File.Exists(filename))
        {
            return File.ReadAllLines(filename);
        }
        else return null;
    }


    /// download file if not already downloaded 
    public static void download_file(Printer print, string url, string dest)
    {
        if(File.Exists(dest))
        {
            print.info($"Already downloaded {dest}");
        }
        else
        {
            print.info($"Downloading {dest}");
            download_file_now(print, url, dest);
        }
    }

    private static void download_file_now(Printer print, string url, string dest)
    {
        using var client = new HttpClient();
        using var s = client.GetStreamAsync(url);
        using var fs = new FileStream(dest, FileMode.OpenOrCreate);
        s.Result.CopyTo(fs);
    }

    /// moves all file from one directory to another
    public static void move_files(Printer print, string from, string to)
    {
        if(Path.Exists(from) == false)
        {
            print.error($"Missing src {from} when moving to {to}");
            return;
        }

        verify_dir_exist(print, to);
        var error = move_files_rec(from, to);
        if (string.IsNullOrEmpty(error) == false)
        {
            print.error($"Failed to move {from} to {to}: {error}");
        }
    }

    public static string move_files_rec(string from, string to)
    {
        var paths = new DirectoryInfo(from);
        
        foreach(var file in paths.EnumerateFiles())
        {
            var src = Path.Join(from, file.Name);
            var dst = Path.Join(to, file.Name);
            File.Move(src, dst);
        }

        foreach(var dir in paths.EnumerateDirectories())
        {
            var src = Path.Join(from, dir.Name);
            var dst = Path.Join(to, dir.Name);
            Directory.CreateDirectory(dst);
            move_files_rec(src, dst);
            Directory.Delete(src, false);
        }

        return "";
    }

    /// extract a zip file to folder
    public static void extract_zip(Printer print, string zip, string to)
    {
        ZipFile.ExtractToDirectory(zip, to);
    }

#if false
    extern crate chrono;
    use chrono::offset::Local;
    use chrono::DateTime;
    use std::time::SystemTime;

    public void display_time(system_time: SystemTime) -> string
    {
        //let system_time = SystemTime::now();
        let datetime: DateTime<Local> = system_time.into();
        datetime.format("%c").to_string()
    }
#endif
}

public readonly record struct SingleReplacement
    (
        string old,
        string neww
    );

/// multi replace calls on a single text 
public class TextReplacer
{
    List<SingleReplacement> replacements = new();

    // add a replacement command 
    public void add(string old, string neww)
    {
        replacements.Add(new SingleReplacement(old, neww));
    }

    public string replace(string in_text)
    {
        var text = in_text;

        foreach (var replacement in replacements)
        {
            text = text.Replace(replacement.old, replacement.neww);
        }

        return text;
    }
}