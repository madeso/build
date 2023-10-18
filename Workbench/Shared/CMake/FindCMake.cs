using System.Collections.Immutable;
using Workbench.Shared;

namespace Workbench.Shared.CMake
{
    internal static class FindCMake
    {
        public static IEnumerable<Found> FindAllInstallations(Log log)
        {
            yield return find_installation_in_registry(log);
            yield return find_installation_in_path(log);

            static Found find_installation_in_registry(Log printer)
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


            static Found find_installation_in_path(Log printer)
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
        }



        public static string? FindInstallationOrNull(Log log)
            => FindAllInstallations(log).GetFirstValueOrNull();



        public static IEnumerable<Found> ListAllBuilds(CompileCommandsArguments settings, Log log)
        {
            const string CMAKE_CACHE_FILE = "CMakeCache.txt";

            yield return find_build_in_current_directory();
            yield return find_build_from_compile_commands(settings, log);
            yield return find_single_build_with_cache();


            static Found find_build_in_current_directory()
            {
                const string SOURCE = "build cache in current dir";

                var build_root = new DirectoryInfo(Environment.CurrentDirectory).FullName;
                if (new FileInfo(Path.Join(build_root, CMAKE_CACHE_FILE)).Exists == false)
                {
                    return new Found(null, SOURCE);
                }

                return new Found(build_root, SOURCE);
            }

            static Found find_build_from_compile_commands(CompileCommandsArguments settings, Log printer)
            {
                var found = settings.GetPathToCompileCommandsOrNull(printer);
                return new Found(found, "compile commands");
            }

            static Found find_single_build_with_cache()
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

        }



        public static string? FindBuildOrNone(CompileCommandsArguments settings, Log log)
            => ListAllBuilds(settings, log).GetFirstValueOrNull();
    }
}