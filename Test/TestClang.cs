using Workbench;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Workbench.Commands.Clang;
using Workbench.Config;
using Workbench.Shared;
using static Workbench.Commands.CheckIncludeOrder.CheckAction;
using Workbench.Shared.CMake;

namespace Test;



public class TestClang : TestBase
{
    [Fact]
    public void empty_generated_clang_tidy_file_should_be_empty()
    {
        var cwd = new Dir(@"C:\test\");
        vfs.AddContent(cwd.GetFile("clang-tidy"), "");
        ClangTidyFile.WriteTidyFileToDisk(vfs, cwd);

        var generated = vfs.GetContent(cwd.GetFile(".clang-tidy"));
        generated.Should().Be("");
    }

    [Fact]
    public async Task if_clang_tidy_is_missing_then_fail()
    {
        var tidy = new ClangTidy();
        var cwd = new Dir(@"C:\test\");
        var paths = new FakePath(new());

        var args = new ClangTidy.Args(null, 1, false, ["libs"], true, false, false, []);
        var ret = await tidy.HandleRunClangTidyCommand(no_run_executor, vfs, paths, cwd, new CompileCommandsArguments(), log, false, args);
        using (new AssertionScope())
        {
            ret.Should().Be(-1);
            log.AllMessages.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task if_has_tidy_and_source_without_error_then_run_successfully()
    {
        var tidy = new ClangTidy();
        var exe = new Dir(@"C:\executables\");
        var cwd = new Dir(@"C:\test\");
        var clang_tidy = exe.GetFile("clang-tidy.exe");
        var paths = new FakePath(new SavedPaths
        {
            ClangTidyExecutable = clang_tidy,
            CompileCommands = cwd.GetFile("compile-commands.json")
        });
        var foobar = cwd.GetSubDirs("src", "libs", "foobar").GetFile("foobar.cc");
        vfs.AddContent(cwd.GetFile("clang-tidy"), "");
        vfs.AddContent(foobar, "");

        AddClangTidyResult(clang_tidy, cwd, foobar, new ProcessExit("", 0));

        var args = new ClangTidy.Args(null, 1, false, ["libs"], false, false, false, []);
        var ret = await tidy.HandleRunClangTidyCommand(exec, vfs, paths, cwd, new CompileCommandsArguments(), log, false, args);
        using (new AssertionScope(log.Print()))
        {
            ret.Should().Be(0);
            log.ErrorsAndWarnings.Should().BeEmpty();
        }
    }

    // todo(Gustav): add clang tidy failures

    private void AddClangTidyResult(Fil clang_tidy, Dir cwd, Fil source, ProcessExit result)
    {
        exec.Add(new(clang_tidy, cwd, 2, source.Path, result));
    }
}
