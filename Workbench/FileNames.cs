namespace Workbench;

internal static class FileNames
{
    // this is stored in /build/ so its meaning is implied
    internal static readonly string BuildSettings = "settings.jsonc";

    internal static readonly string ClangTidyStore = ".workbench.clang-tidy-store.jsonc";

    internal static readonly string BuildData = "workbench.build.jsonc";
    internal static readonly string Includes = "workbench.check-includes.jsonc";
    internal static readonly string CheckNames = "workbench.check-names.jsonc";
}
