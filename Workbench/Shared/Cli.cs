using System.Collections.Immutable;
using System.Globalization;
using Spectre.Console;
using Workbench.Shared.Extensions;
using static Workbench.Commands.Indent.IndentationCommand;

namespace Workbench.Shared;

public static class Cli
{
    public const string STDOUT_ARGUMENT = "stdout";

    public static async Task WriteFileAsync(string file, IEnumerable<string> lines)
    {
        if (file.ToLower() == STDOUT_ARGUMENT)
        {
            foreach (var l in lines)
            {
                AnsiConsole.WriteLine(l);
            }
        }
        else
        {
            var output = lines.ToImmutableArray();
            AnsiConsole.MarkupLineInterpolated($"Writing {output.Length} lines of dot to {file}");
            await File.WriteAllLinesAsync(file, output);
        }
    }

    public static Dir? RequireDirectory(Dir cwd, Log log, string? arg, string name)
    {
        if (string.IsNullOrEmpty(arg)) return cwd;

        var rooted = FileUtil.RootPath(cwd, arg);

        var dd = new Dir(rooted);
        if (dd.Exists) return dd;

        var file = new Fil(rooted);
        if (!file.Exists)
        {
            log.Error($"Directory {dd} for {name} doesn't exist");
            return null;
        }

        var fd = file.Directory;
        if (fd != null)
        {
            return fd;
        }

        log.Error($"Directory {dd} doesn't exist and failed to get directory from file {file} for {name}");
        return null;
    }

    public static IEnumerable<Dir> ToDirectories(Dir cwd, Log log, IEnumerable<string> args)
    {
        return args.Select(a => new { Arg = a, Folder = ToExistingDirOrNull(cwd, a) })
            .SelectNonNull(f => f.Folder, f =>
            {
                log.Warning($"{f.Arg} is not a directory");
            });
    }

    public static Dir? ToDirPath(Dir cwd, string a)
    {
        if (string.IsNullOrEmpty(a)) return null;
        var p = FileUtil.RootPath(cwd, a);
        if(p == null) return null;
        return new Dir(p);
    }

    private static Dir? ToExistingDirOrNull(Dir cwd, string a)
    {
        var p = ToDirPath(cwd, a);
        if(p == null) return null;
        if(p.Exists == false) return null;
        return p;
    }

    public static IEnumerable<Fil> ToFiles(Log log, IEnumerable<string> args)
    {
        return args.Select(a => new {Arg = a, File = Fil.ToExistingDirOrNull(a)})
            .SelectNonNull(f => f.File, f =>
            {
                log.Warning($"{f.Arg} is not a file");
            });
    }

    public static Fil ToSingleFile(Dir cwd, string arg, string name_if_missing)
    {
        if (string.IsNullOrEmpty(arg))
        {
            return cwd.GetFile(name_if_missing);
        }

        var rooted = FileUtil.RootPath(cwd, arg);
        if (Directory.Exists(rooted))
        {
            return new Dir(rooted).GetFile(name_if_missing);
        }

        return new Fil(rooted);
    }

    public static Fil? RequireFile(Dir cwd, Log log, string arg, string name)
    {
        var file = new Fil(FileUtil.RootPath(cwd, arg));
        if (file.Exists == false)
        {
            log.Error($"File '{arg}', passed for {name}, doesn't exist ({file})");
            return null;
        }

        return file;
    }

    public static FileOrDir[]? ToExistingFileOrDir(Dir cwd, IEnumerable<string>? args, Log log)
    {
        bool ok = true;
        var ret = args
                ?.Select(a => new { Arg = a, Resolved = FileUtil.RootPath(cwd, a) })
                ?.Select(a => new { a.Arg, a.Resolved, Ret = FileOrDir.FromExistingOrNull(a.Resolved) })
                ?.Where(f => f.Ret is { Exists: true }, f =>
                {
                    log.Error($"{f.Arg} is neither a file nor a directory (resolved to {f.Resolved})");
                    ok = false;
                })
                .Select(f => f.Ret!)
                .ToArray()
            ;

        if (ok == false)
        {
            return null;
        }

        if (ret == null || ret.Length == 0)
        {
            return Array.Empty<FileOrDir>();
        }

        return ret;
    }

    
    public static string GetValueOrDefault(string value, string def)
    {
        var vt = value.Trim();
        return vt.Trim() == "" || vt.Trim() == "?" ? def : value;
    }

    public static Fil GetValueOrDefault(string value, Fil def)
    {
        var vt = value.Trim();
        return vt.Trim() == "" || vt.Trim() == "?" ? def : new Fil(value);
    }

    public static Markup ToMarkup(FormattableString value)
    {
        var provider = CultureInfo.CurrentCulture;
        return new Markup(escape_interpolated(provider, value));

        static string escape_interpolated(CultureInfo ci, FormattableString value)
        {
            object?[] args = value.GetArguments().Select(arg => arg is string s ? s.EscapeMarkup() : arg)
                .ToArray();
            return string.Format(ci, value.Format, args);
        }
    }

    public static Dir ToOutputDirectory(Dir cwd, string arg)
    {
        if (string.IsNullOrEmpty(arg)) return cwd;

        var rooted = FileUtil.RootPath(cwd, arg);

        return new Dir(rooted);
    }
}