using System;
using System.Collections.Immutable;
using System.Linq;
using Workbench.Shared;
using Workbench.Shared.Extensions;

namespace Workbench.Shared.CMake
{
    internal static class FindCMake
    {
        public static IEnumerable<Found<Fil>> FindAllInstallations()
        {
            yield return find_installation_in_registry();
            yield return find_installation_in_path();

            static Found<Fil> find_installation_in_registry()
            {
                const string REGISTRY_SOURCE = "registry";

                var install_dir = Registry.Hklm(@"SOFTWARE\Kitware\CMake", "InstallDir");
                if (install_dir == null)
                {
                    return Found<Fil>.Fail($"Failed to find in {install_dir}", REGISTRY_SOURCE);
                }

                var path = Path.Join(install_dir, "bin", "cmake.exe");
                if (File.Exists(path) == false)
                {
                    return Found<Fil>.Fail($"Found path to cmake in registry ({path}) but it didn't exist", REGISTRY_SOURCE);
                }

                return Found<Fil>.Success(new Fil(path), REGISTRY_SOURCE);
            }


            static Found<Fil> find_installation_in_path()
            {
                return Which.FindPaths("cmake");
            }
        }

        public static Fil? RequireInstallationOrNull(Log log)
            => FindAllInstallations().RequireFirstValueOrNull(log, "cmake");

        public static Fil? FindInstallationOrNull()
            => FindAllInstallations().GetFirstValueOrNull();


        private static IEnumerable<Found<Dir>> FindJustTheBuilds()
        {
            const string CMAKE_CACHE_FILE = "CMakeCache.txt";

            var cwd = Dir.CurrentDirectory;
            yield return Functional
                .Params(find_cmake_cache(cwd))
                .Collect("build cache in current dir");
            yield return FileUtil.PitchforkBuildFolders(cwd)
                .Select(find_cmake_cache)
                .Collect("pitchfork roots");
            yield break;

            static FoundEntry<Dir> find_cmake_cache(Dir build_root)
            {
                var cache_file = build_root.GetFile(CMAKE_CACHE_FILE);
                if (cache_file.Exists == false)
                {
                    return new FoundEntry<Dir>.Error($"{cache_file} doesn't exist");
                }

                return new FoundEntry<Dir>.Result(build_root);
            }
        }

        // todo(Gustav): don't reuse compile commands folder?
        private static FoundEntry<Dir>? GetBuildFromArgument(CompileCommandsArguments settings)
        {
            return settings.GetDirectoryFromArgument();
        }

        public static IEnumerable<Found<Dir>> ListAllBuilds(CompileCommandsArguments settings)
        {
            return Functional.Params(
                    Functional.Params(GetBuildFromArgument(settings))
                        .IgnoreNull()
                        .Collect("commandline")
                    )
                .Concat(FindJustTheBuilds());
        }

        public static Dir? RequireBuildOrNone(CompileCommandsArguments settings, Log log)
        {
            var found = FindBuildOrNone(settings, log);
            if (found == null)
            {
                log.Error("No build cache folder specified or none/too many found");
            }

            return found;
        }

        public static Dir? FindBuildOrNone(CompileCommandsArguments settings, Log? log)
        {
            // commandline overrides all builds
            var arg = GetBuildFromArgument(settings);
            switch (arg)
            {
                case FoundEntry<Dir>.Result r:
                    return r.Value;
                case FoundEntry<Dir>.Error e:
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