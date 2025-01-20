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
        var ret = await tidy.HandleRunClangTidyCommand(vfs, paths, cwd, new CompileCommandsArguments(), log, false, args);
        using (new AssertionScope())
        {
            ret.Should().Be(-1);
            log.Errors.Should().BeEmpty();
        }
    }

    // currently fails
    //*
    [Fact]
    public async Task if_has_tidy_then_run_it()
    {
        var tidy = new ClangTidy();
        var exe = new Dir(@"C:\executables\");
        var cwd = new Dir(@"C:\test\");
        var paths = new FakePath(new SavedPaths
        {
            ClangTidyExecutable = exe.GetFile("clang-tidy.exe"),
            CompileCommands = cwd.GetFile("compile-commands.json")
        });
        vfs.AddContent(cwd.GetFile("clang-tidy"), "");
        vfs.AddContent(cwd.GetSubDirs("src", "libs", "foobar").GetFile("foobar.cc"), "");

        // todo(Gustav): disable NOP
        var args = new ClangTidy.Args(null, 1, false, ["libs"], true, false, false, []);
        var ret = await tidy.HandleRunClangTidyCommand(vfs, paths, cwd, new CompileCommandsArguments(), log, false, args);
        using (new AssertionScope())
        {
            ret.Should().Be(0);
            log.Errors.Should().BeEmpty();
        }
    }
    //*/
}
