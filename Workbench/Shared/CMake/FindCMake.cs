using System;
using System.Collections.Immutable;
using System.Linq;
using Workbench.Shared;
using Workbench.Shared.Extensions;

namespace Workbench.Shared.CMake
{
    internal static class FindCMake
    {
        public static IEnumerable<Found<Fil>> FindAllInstallations(Vfs vfs)
        {
            yield return find_installation_in_registry();
            yield return find_installation_in_path(vfs);

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


            static Found<Fil> find_installation_in_path(Vfs vfs)
            {
                return Which.FindPaths(vfs, "cmake");
            }
        }

        public static Fil? RequireInstallationOrNull(Vfs vfs, Log log)
            => FindAllInstallations(vfs).RequireFirstValueOrNull(log, "cmake");

        public static Fil? FindInstallationOrNull(Vfs vfs)
            => FindAllInstallations(vfs).GetFirstValueOrNull();


        private static IEnumerable<Found<Dir>> FindJustTheBuilds(Vfs vfs, Dir cwd)
        {
            const string CMAKE_CACHE_FILE = "CMakeCache.txt";

            yield return Functional
                .Params(find_cmake_cache(vfs, cwd))
                .Collect("build cache in current dir");
            yield return FileUtil.PitchforkBuildFolders(vfs, cwd)
                .Select(d => find_cmake_cache(vfs, d))
                .Collect("pitchfork roots");
            yield break;

            static FoundEntry<Dir> find_cmake_cache(Vfs vfs, Dir build_root)
            {
                var cache_file = build_root.GetFile(CMAKE_CACHE_FILE);
                if (cache_file.Exists(vfs) == false)
                {
                    return new FoundEntry<Dir>.Error($"{cache_file} doesn't exist");
                }

                return new FoundEntry<Dir>.Result(build_root);
            }
        }

        // todo(Gustav): don't reuse compile commands folder?
        private static FoundEntry<Dir>? GetBuildFromArgument(Vfs vfs, CompileCommandsArguments settings)
        {
            return settings.GetDirectoryFromArgument(vfs);
        }

        public static IEnumerable<Found<Dir>> ListAllBuilds(Vfs vfs, Dir cwd, CompileCommandsArguments settings)
        {
            return Functional.Params(
                    Functional.Params(GetBuildFromArgument(vfs, settings))
                        .IgnoreNull()
                        .Collect("commandline")
                    )
                .Concat(FindJustTheBuilds(vfs, cwd));
        }

        public static Dir? RequireBuildOrNone(Vfs vfs, Dir cwd, CompileCommandsArguments settings, Log log)
        {
            var found = FindBuildOrNone(vfs, cwd, settings, log);
            if (found == null)
            {
                log.Error("No build cache folder specified or none/too many found");
            }

            return found;
        }

        public static Dir? FindBuildOrNone(Vfs vfs, Dir cwd, CompileCommandsArguments settings, Log? log)
        {
            // commandline overrides all builds
            var arg = GetBuildFromArgument(vfs, settings);
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

            var valid = FindJustTheBuilds(vfs, cwd).AllValid().ToImmutableArray();

            // only one build folder is valid
            return valid.Length == 1
                ? valid[0]
                : null;
        }
    }
}