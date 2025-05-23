using Spectre.Console;

namespace Workbench.Shared.CMake;

// cmake generator
public class Generator
{
    public Generator(string name, string? arch = null)
    {
        Name = name;
        Arch = arch;
    }

    public string Name { get; }
    public string? Arch { get; }
}

public enum Config
{
    Debug, Release
}

public enum Install
{
    No, Yes
}

// utility to call cmake commands on a project
public class CMakeProject
{
    public CMakeProject(Dir build_folder, Dir source_folder, Generator generator)
    {
        this.generator = generator;
        this.build_folder = build_folder;
        this.source_folder = source_folder;
    }

    private readonly Generator generator;
    private readonly Dir build_folder;
    private readonly Dir source_folder;
    private readonly List<Argument> arguments = new();

    // a cmake argument
    private class Argument
    {
        public Argument(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public Argument(string name, string value, string typename) : this(name, value)
        {
            TypeName = typename;
        }

        // format for commandline
        public string FormatForCmakeArgument()
        {
            if (TypeName == null)
            {
                return $"-D{Name}={Value}";
            }
            else
            {
                return $"-D{Name}:{TypeName}={Value}";
            }
        }

        private string Name { get; }
        private string Value { get; }
        private string? TypeName { get; }
    }

    // add argument with a explicit type set
    private void AddArgumentWithType(string name, string value, string typename)
    {
        arguments.Add(new Argument(name, value, typename));
    }

    // add argument
    public void AddArgument(string name, string value)
    {
        arguments.Add(new Argument(name, value));
    }

    public void AddArgumentWithEscape(string name, Dir value)
    {
        var p = value.Path.Replace('\\', '/');
        if (p.EndsWith('/') == false) { p += '/'; }
        arguments.Add(new Argument(name, p));
    }



    // set the install folder
    public void SetInstallFolder(Dir folder)
    {
        AddArgumentWithType("CMAKE_INSTALL_PREFIX", folder.Path, "PATH");
    }

    // set cmake to make static (not shared) library
    public void MakeStaticLibrary()
    {
        AddArgument("BUILD_SHARED_LIBS", "0");
    }

    // run cmake configure step
    public async Task ConfigureAsync(Executor exec, Vfs vfs, Dir cwd, Log log, bool nop = false)
    {
        var cmake = FindCMake.RequireInstallationOrNull(vfs, log);
        if (cmake == null)
        {
            return;
        }

        var command = new ProcessBuilder(cmake);
        foreach (var argument in arguments.Select(arg => arg.FormatForCmakeArgument()))
        {
            AnsiConsole.WriteLine($"Setting CMake argument for config: {argument}");
            command.AddArgument(argument);
        }

        command.AddArgument(source_folder.Path);
        command.AddArgument("-G");
        command.AddArgument(generator.Name);
        if (generator.Arch != null)
        {
            command.AddArgument("-A");
            command.AddArgument(generator.Arch);
        }

        Core.VerifyDirectoryExists(vfs, log, build_folder);
        command.WorkingDirectory = build_folder;

        if (Core.IsWindows())
        {
            if (nop)
            {
                AnsiConsole.WriteLine($"Configuring cmake: {command}");
            }
            else
            {
                await command.RunAndPrintOutputAsync(exec, cwd, log);
            }
        }
        else
        {
            AnsiConsole.WriteLine($"Configuring cmake: {command}");
        }
    }

    // run cmake build step
    private async Task RunBuildCommandAsync(Executor exec, Vfs vfs, Dir cwd, Log log, Install install, Config config)
    {
        var cmake = FindCMake.RequireInstallationOrNull(vfs, log);
        if (cmake == null)
        {
            log.Error("CMake executable not found");
            return;
        }

        var command = new ProcessBuilder(cmake);
        command.AddArgument("--build");
        command.AddArgument(".");

        if (install == CMake.Install.Yes)
        {
            command.AddArgument("--target");
            command.AddArgument("install");
        }
        command.AddArgument("--config");
        command.AddArgument(config switch
        {
            Config.Debug => "Debug",
            Config.Release => "Release",
            _ => throw new NotImplementedException(),
        });

        Core.VerifyDirectoryExists(vfs, log, build_folder);
        command.WorkingDirectory = build_folder;

        if (Core.IsWindows())
        {
            await command.RunAndPrintOutputAsync(exec, cwd, log);
        }
        else
        {
            AnsiConsole.WriteLine($"Found path to cmake in path ({command}) but it didn't exist");
        }
    }

    // build cmake project
    public async Task BuildAsync(Executor exec, Vfs vfs, Dir cwd, Log log, Config config)
    {
        await RunBuildCommandAsync(exec, vfs, cwd, log, CMake.Install.No, config);
    }

    // install cmake project
    public async Task InstallAsync(Executor exec, Vfs vfs, Dir cwd, Log log, Config config)
    {
        await RunBuildCommandAsync(exec, vfs, cwd, log, CMake.Install.Yes, config);
    }
}