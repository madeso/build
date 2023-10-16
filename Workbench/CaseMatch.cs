using System.Text.RegularExpressions;

namespace Workbench;


internal static partial class CaseMatch
{
    public static bool IsLowerSnakeCase(string name) => lower_snake_case_regex.IsMatch(name);
    private static readonly Regex lower_snake_case_regex = GenerateLowerSnakeCase();
    [GeneratedRegex("^[a-z]([a-z0-9]*(_[a-z0-9]+)*)?$", RegexOptions.Compiled)]
    private static partial Regex GenerateLowerSnakeCase();

    public static bool IsUpperSnakeCase(string name) => upper_snake_case_regex.IsMatch(name);
    private static readonly Regex upper_snake_case_regex = GenerateUpperSnakeCase();
    [GeneratedRegex("^[A-Z]([A-Z0-9]*(_[A-Z0-9]+)*)?$", RegexOptions.Compiled)]
    private static partial Regex GenerateUpperSnakeCase();

    public static bool IsCamelCase(string name) =>
        // handle special case where all characters are uppercase, this is not valid
        name == name.ToUpperInvariant() ? false
            : camel_case_regex.IsMatch(name);
    private static readonly Regex camel_case_regex = GenerateCamelCase();
    [GeneratedRegex("^[A-Z]([a-z0-9]+([A-Z0-9][a-z0-9]*)*)?$", RegexOptions.Compiled)]
    private static partial Regex GenerateCamelCase();

    public static bool IsTemplateName(string name) => template_name_regex.IsMatch(name);
    private static readonly Regex template_name_regex = GenerateTemplateName();
    [GeneratedRegex("^T[A-Z][a-z0-9]+([A-Z0-9][a-z0-9]+)*$", RegexOptions.Compiled)]
    private static partial Regex GenerateTemplateName();

}

