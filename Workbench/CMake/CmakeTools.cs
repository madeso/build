using System.Collections.Immutable;
using Workbench.Utils;

namespace Workbench.CMake
{
    internal static class CmakeTools
    {
        private const string CMAKE_CACHE_FILE = "CMakeCache.txt";

        private static Found FindInstallationInRegistry(Printer printer)
        {
            const string REGISTRY_SOURCE = "registry";

            var install_dir = Registry.Hklm(@"SOFTWARE\Kitware\CMake", "InstallDir");
            if (install_dir == null) { return new Found(null, REGISTRY_SOURCE); }

            var path = Path.Join(install_dir, "bin", "cmake.exe");
            if (File.Exists(path) == false)
            {
                printer.Error($"Found path to cmake in registry ({path}) but it didn't exist");
                return new Found(null, REGISTRY_SOURCE);
            }

            return new Found(path, REGISTRY_SOURCE);
        }


        private static Found FindInstallationInPath(Printer printer)
        {
            const string PATH_SOURCE = "path";
            var path = Which.Find("cmake");
            if (path == null)
            {
                return new Found(null, PATH_SOURCE);
            }

            if (File.Exists(path) == false)
            {
                printer.Error($"Found path to cmake in path ({path}) but it didn't exist");
                return new Found(null, PATH_SOURCE);
            }
            return new Found(path, PATH_SOURCE);
        }


        public static IEnumerable<Found> FindAllInstallations(Printer printer)
        {
            yield return FindInstallationInRegistry(printer);
            yield return FindInstallationInPath(printer);
        }


        public static string? FindInstallationOrNull(Printer printer)
        {
            return FoundExtensions.GetFirstValueOrNull(FindAllInstallations(printer));
        }


        public static Found FindBuildInCurrentDirectory()
        {
            var source = "current dir";

            var build_root = new DirectoryInfo(Environment.CurrentDirectory).FullName;
            if (new FileInfo(Path.Join(build_root, CMAKE_CACHE_FILE)).Exists == false)
            {
                return new Found(null, source);
            }

            return new Found(build_root, source);
        }

        public static Found FindBuildFromCompileCommands(CompileCommandsArguments settings, Printer printer)
        {
            var found = settings.GetPathToCompileCommandsOrNull(printer);
            return new Found(found, "compile commands");
        }

        public static Found FindSingleBuildWithCache()
        {
            var cwd = Environment.CurrentDirectory;
            var roots = FileUtil.PitchforkBuildFolders(cwd)
                .Where(root => new FileInfo(Path.Join(root, CMAKE_CACHE_FILE)).Exists)
                .ToImmutableArray();

            return roots.Length switch
            {
                0 => new Found(null, "no builds found from cache"),
                1 => new Found(roots[0], "build root with cache"),
                _ => new Found(null, "too many builds found from cache"),
            };
        }

        public static IEnumerable<Found> ListAllBuilds(CompileCommandsArguments settings, Printer printer)
        {
            yield return FindBuildInCurrentDirectory();
            yield return FindBuildFromCompileCommands(settings, printer);
            yield return FindSingleBuildWithCache();
        }


        public static string? FindBuildOrNone(CompileCommandsArguments settings, Printer printer)
        {
            return FoundExtensions.GetFirstValueOrNull(ListAllBuilds(settings, printer));
        }
    }
}