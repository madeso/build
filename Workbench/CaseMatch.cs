using System.Text.RegularExpressions;

namespace Workbench;


internal static partial class CaseMatch
{
    // CamelCase.IsMatch(name)

    public static bool LowerSnakeCase(string name) => LowerSnakeCaseRegex.IsMatch(name);
    private static readonly Regex LowerSnakeCaseRegex = GenerateLowerSnakeCase();
    [GeneratedRegex("^[a-z]([a-z0-9]*(_[a-z0-9]+)*)?$", RegexOptions.Compiled)]
    private static partial Regex GenerateLowerSnakeCase();

    public static bool UpperSnakeCase(string name) => UpperSnakeCaseRegex.IsMatch(name);
    private static readonly Regex UpperSnakeCaseRegex = GenerateUpperSnakeCase();
    [GeneratedRegex("^[A-Z]([A-Z0-9]*(_[A-Z0-9]+)*)?$", RegexOptions.Compiled)]
    private static partial Regex GenerateUpperSnakeCase();

    public static bool CamelCase(string name)
    {
        // handle special case where all characters are uppercase, this is not valid
        if(name == name.ToUpperInvariant()) { return false; }

        return CamelCaseRegex.IsMatch(name);
    }
    private static readonly Regex CamelCaseRegex = GenerateCamelCase();
    [GeneratedRegex("^[A-Z]([a-z0-9]+([A-Z0-9][a-z0-9]*)*)?$", RegexOptions.Compiled)]
    private static partial Regex GenerateCamelCase();

    public static bool TemplateName(string name) => TemplateNameRegex.IsMatch(name);
    private static readonly Regex TemplateNameRegex = GenerateTemplateName();
    [GeneratedRegex("^T[A-Z][a-z0-9]+([A-Z0-9][a-z0-9]+)*$", RegexOptions.Compiled)]
    private static partial Regex GenerateTemplateName();

}

