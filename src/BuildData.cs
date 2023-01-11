namespace Workbench;


using Newtonsoft.Json;
using System.IO;
using System.Text.RegularExpressions;
using static Workbench.Core;

class ProjectFile
{
    [JsonProperty("name")]
    public string name = "";

    [JsonProperty("dependencies")]
    public List<DependencyName> dependencies = new();

    [JsonProperty("includes")]
    public List<List<string>> includes = new();
}

public interface OptionalRegex
{
    Regex? GetRegex(Printer print, TextReplacer replacer);
}

public class OptionalRegexDynamic : OptionalRegex
{
    readonly string regex;

    public OptionalRegexDynamic(string regex)
    {
        this.regex = regex;
    }

    public Regex? GetRegex(Printer print, TextReplacer replacer)
    {
        var regex_source = replacer.replace(regex);
        switch(BuildData.CompileRegex(regex_source))
        {
            case RegexOrErr.Value re:
                return re.regex;
            case RegexOrErr.Error error:
                print.error($"{regex} -> {regex_source} is invalid regex: {error.error}");
                return null;
            default:
                throw new ArgumentException("invalid state");
        }
    }
}

public class OptionalRegexStatic : OptionalRegex
{
    readonly Regex regex;

    public OptionalRegexStatic(Regex regex)
    {
        this.regex = regex;
    }

    public Regex? GetRegex(Printer print, TextReplacer replacer)
    {
        return regex;
    }
}

public class OptionalRegexFailed : OptionalRegex
{
    readonly string error;

    public OptionalRegexFailed(string error)
    {
        this.error = error;
    }

    public Regex? GetRegex(Printer print, TextReplacer replacer)
    {
        print.error(error);
        return null;
    }
}

abstract record RegexOrErr
{
    public record Value(Regex regex) : RegexOrErr;
    public record Error(string error) : RegexOrErr;
}


// #[derive(Debug)]
public struct BuildData
{
    public string name;
    public List<Dependency> dependencies;
    public string root_dir;
    public string build_base_dir;
    public string build_dir;
    public string dependency_dir;
    public List<List<OptionalRegex>> includes;

    private static IEnumerable<OptionalRegex> strings_to_regex(TextReplacer replacer, IEnumerable<string> includes, Printer print)
    {
        return includes.Select
        (
            (Func<string, OptionalRegex>)(regex =>
            {
                var regex_source = replacer.replace(regex);
                if (regex_source != regex)
                {
                    return new OptionalRegexDynamic(regex);
                }
                else
                {
                    switch(CompileRegex(regex_source))
                    {
                        case RegexOrErr.Value re:
                            return new OptionalRegexStatic(re.regex);

                        case RegexOrErr.Error err:
                            var error = $"{regex} is invalid regex: {err.error}";
                            print.error(error);
                            return new OptionalRegexFailed(error);
                        default:
                            throw new Exception("unhandled case");
                    }
                }
            })
        );
    }

    internal static RegexOrErr CompileRegex(string regex_source)
    {
        try
        {
            return new RegexOrErr.Value(new Regex(regex_source, RegexOptions.Compiled));
        }
        catch (ArgumentException err)
        {
            return new RegexOrErr.Error(err.Message);
        }
    }

    public BuildData(string name, string root_dir, List<List<string>> includes, Printer print)
    {
        this.name = name;
        this.dependencies = new();
        this.root_dir = root_dir;
        this.build_base_dir = Path.Join(root_dir, "build");
        this.build_dir = Path.Join(build_base_dir, name);
        this.dependency_dir = Path.Join(build_base_dir, "deps");

        var replacer = CheckIncludes.IncludeTools.get_replacer("file_stem");
        this.includes = includes.Select(includes => strings_to_regex(replacer, includes, print).ToList()).ToList();
    }


    // get the path to the settings file
    public string get_path_to_settings()
    {
        return Path.Join(build_base_dir, "settings.json");
    }

    public static BuildData? load_from_dir(string root, Printer print)
    {
        var file = Path.Join(root, "project.wb.json");
        if(File.Exists(file) == false)
        {
            print.error($"Unable to read file: {file}");
            return null;
        }
        var content = File.ReadAllText(file);
        var loaded = JsonUtil.Parse<ProjectFile>(print, file, content);
        if(loaded == null)
        {
            return null;
        }

        var bd = new BuildData(loaded.name, root, loaded.includes, print);
        foreach(var dependency_name in loaded.dependencies)
        {
            bd.dependencies.Add(Dependencies.CreateDependency(dependency_name, bd));
        }
        return bd;
    }

    public static BuildData? load(Printer print)
    {
        return load_from_dir(Environment.CurrentDirectory, print);
    }

}