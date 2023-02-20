using System.Text.RegularExpressions;

namespace Workbench;


internal static partial class CaseMatch
{
    public static readonly Regex LowerSnakeCase = GenerateLowerSnakeCase();
    [GeneratedRegex("^[a-z][a-z0-9]+(_[a-z0-9]+)*$", RegexOptions.Compiled)]
    private static partial Regex GenerateLowerSnakeCase();

    public static readonly Regex UpperSnakeCase = GenerateUpperSnakeCase();
    [GeneratedRegex("^[A-Z][A-Z0-9]+(_[A-Z0-9]+)*$", RegexOptions.Compiled)]
    private static partial Regex GenerateUpperSnakeCase();

    public static readonly Regex CamelCase = GenerateCamelCase();
    [GeneratedRegex("^[A-Z][a-z0-9]+([A-Z0-9][a-z0-9]+)*$", RegexOptions.Compiled)]
    private static partial Regex GenerateCamelCase();

}

