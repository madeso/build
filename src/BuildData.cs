namespace Workbench;


using Newtonsoft.Json;
using System.IO;
using System.Text.RegularExpressions;

class ProjectFile
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("dependencies")]
    public List<DependencyName> Dependencies { get; } = new();

    [JsonProperty("includes")]
    public List<List<string>> IncludeDirectories { get; } = new();
}

public interface OptionalRegex
{
    Regex? GetRegex(Printer print, TextReplacer replacer);
}

public class OptionalRegexDynamic : OptionalRegex
{
    private readonly string regex;

    public OptionalRegexDynamic(string regex)
    {
        this.regex = regex;
    }

    public Regex? GetRegex(Printer print, TextReplacer replacer)
    {
        var regexSource = replacer.Replace(regex);
        switch (BuildData.CompileRegex(regexSource))
        {
            case RegexOrErr.Value re:
                return re.regex;
            case RegexOrErr.Error error:
                print.error($"{regex} -> {regexSource} is invalid regex: {error.error}");
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


public struct BuildData
{
    public string name;
    public List<Dependency> Dependencies;
    public string RootDirectory;
    public string BuildDirectory;
    public string ProjectDirectory;
    public string DependencyDirectory;
    public List<List<OptionalRegex>> IncludeDirectories;

    private static IEnumerable<OptionalRegex> strings_to_regex(TextReplacer replacer, IEnumerable<string> includes, Printer print)
    {
        return includes.Select
        (
            (Func<string, OptionalRegex>)(regex =>
            {
                var regex_source = replacer.Replace(regex);
                if (regex_source != regex)
                {
                    return new OptionalRegexDynamic(regex);
                }
                else
                {
                    switch (CompileRegex(regex_source))
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
        this.Dependencies = new();
        this.RootDirectory = root_dir;
        this.BuildDirectory = Path.Join(root_dir, "build");
        this.ProjectDirectory = Path.Join(BuildDirectory, name);
        this.DependencyDirectory = Path.Join(BuildDirectory, "deps");

        var replacer = CheckIncludes.IncludeTools.get_replacer("file_stem");
        this.IncludeDirectories = includes.Select(includes => strings_to_regex(replacer, includes, print).ToList()).ToList();
    }


    // get the path to the settings file
    public string get_path_to_settings()
    {
        return Path.Join(BuildDirectory, "settings.json");
    }

    public static BuildData? load_from_dir(string root, Printer print)
    {
        string file = GetBuildDataPath(root);
        if (File.Exists(file) == false)
        {
            print.error($"Unable to read file: {file}");
            return null;
        }
        var content = File.ReadAllText(file);
        var loaded = JsonUtil.Parse<ProjectFile>(print, file, content);
        if (loaded == null)
        {
            return null;
        }

        var bd = new BuildData(loaded.Name, root, loaded.IncludeDirectories, print);
        foreach (var dependency_name in loaded.Dependencies)
        {
            bd.Dependencies.Add(Workbench.Dependencies.CreateDependency(dependency_name, bd));
        }
        return bd;
    }

    public static string GetBuildDataPath(string root)
    {
        return Path.Join(root, "project.wb.json");
    }

    public static BuildData? load(Printer print)
    {
        return load_from_dir(Environment.CurrentDirectory, print);
    }

}