using System;
using System.Collections.Immutable;
using System.Linq;
using Workbench.Shared;
using Workbench.Shared.Extensions;

namespace Workbench.Shared.CMake
{
    internal static class FindCMake
    {
        public static IEnumerable<Found<string>> FindAllInstallations()
        {
            yield return find_installation_in_registry();
            yield return find_installation_in_path();

            static Found<string> find_installation_in_registry()
            {
                const string REGISTRY_SOURCE = "registry";

                var install_dir = Registry.Hklm(@"SOFTWARE\Kitware\CMake", "InstallDir");
                if (install_dir == null)
                {
                    return Found<string>.Fail($"Failed to find in {install_dir}", REGISTRY_SOURCE);
                }

                var path = Path.Join(install_dir, "bin", "cmake.exe");
                if (File.Exists(path) == false)
                {
                    return Found<string>.Fail($"Found path to cmake in registry ({path}) but it didn't exist", REGISTRY_SOURCE);
                }

                return Found<string>.Success(path, REGISTRY_SOURCE);
            }


            static Found<string> find_installation_in_path()
            {
                return Which.FindPaths("cmake");
            }
        }

        public static string? RequireInstallationOrNull(Log log)
        {
            var found = FindInstallationOrNull();
            if (found == null)
            {
                log.Error("Failed to find a valid cmake");
            }

            return found;
        }


        public static string? FindInstallationOrNull()
            => FindAllInstallations().GetFirstValueOrNull();

        private static IEnumerable<Found<string>> FindJustTheBuilds()
        {
            const string CMAKE_CACHE_FILE = "CMakeCache.txt";

            var cwd = new DirectoryInfo(Environment.CurrentDirectory).FullName;
            yield return Functional
                .Params(find_cmake_cache(cwd))
                .Collect("build cache in current dir");
            yield return FileUtil.PitchforkBuildFolders(cwd)
                .Select(find_cmake_cache)
                .Collect("pitchfork roots");

            static FoundEntry<string> find_cmake_cache(string build_root)
            {
                var cache_file = Path.Join(build_root, CMAKE_CACHE_FILE);
                if (new FileInfo(cache_file).Exists == false)
                {
                    return new FoundEntry<string>.Error($"{cache_file} doesn't exist");
                }

                return new FoundEntry<string>.Result(build_root);
            }
        }

        // todo(Gustav): don't reuse compile commands folder?
        private static FoundEntry<string>? GetBuildFromArgument(CompileCommandsArguments settings)
        {
            return settings.GetDirectoryFromArgument();
        }

        public static IEnumerable<Found<string>> ListAllBuilds(CompileCommandsArguments settings)
        {
            return Functional.Params(
                    Functional.Params(GetBuildFromArgument(settings))
                        .IgnoreNull()
                        .Collect("commandline")
                    )
                .Concat(FindJustTheBuilds());
        }

        public static string? RequireBuildOrNone(CompileCommandsArguments settings, Log log)
        {
            var found = FindBuildOrNone(settings, log);
            if (found == null)
            {
                log.Error("No build cache folder specified or none/too many found");
            }

            return found;
        }

        public static string? FindBuildOrNone(CompileCommandsArguments settings, Log? log)
        {
            // commandline overrides all builds
            var arg = GetBuildFromArgument(settings);
            switch (arg)
            {
                case FoundEntry<string>.Result r:
                    return r.Value;
                case FoundEntry<string>.Error e:
                {
                    log?.Error(e.Reason);
                    return null;
                }
            }

            var valid = FindJustTheBuilds().AllValid().ToImmutableArray();

            // only one build folder is valid
            return valid.Length == 1
                ? valid[0]
                : null;
        }
    }
}