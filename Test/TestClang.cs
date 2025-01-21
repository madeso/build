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
        vfs.AddContent(cwd.GetFile(".workbench.clang-tidy-store.jsonc"), "");

        AddClangTidyResult(clang_tidy, cwd, foobar, 0);

        var args = new ClangTidy.Args(null, 1, false, ["libs"], false, false, false, []);
        var ret = await tidy.HandleRunClangTidyCommand(exec, vfs, paths, cwd, new CompileCommandsArguments(), log, false, args);
        using (new AssertionScope(log.Print()))
        {
            ret.Should().Be(0);
            log.ErrorsAndWarnings.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task if_has_tidy_and_source_error_then_it_should_be_displayed()
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
        vfs.AddContent(cwd.GetFile(".workbench.clang-tidy-store.jsonc"), "");
        vfs.AddContent(foobar, "");

        AddClangTidyResult(clang_tidy, cwd, foobar, -1,
            @"C:\test\src\libs\foobar.cc:7:1: warning: function 'CATCH2_INTERNAL_TEST_0' declared 'static', move to anonymous namespace instead [misc-use-anonymous-namespace]",
            "    7 | TEST_CASE(hs-hash, [hash])",
            "      | ^",
            @"C:\test\src\libs\foobar.hpp:144:28: note: expanded from macro 'TEST_CASE'",
            "  144 |   #define TEST_CASE( ... ) INTERNAL_CATCH_TESTCASE( __VA_ARGS__ )",
            "      |                            ^",
            "note: expanded from here",
            @"C:\test\src\libs\foobar.cc:13:1: warning: function 'CATCH2_INTERNAL_TEST_2' declared 'static', move to anonymous namespace instead [misc-use-anonymous-namespace]",
            "   13 | TEST_CASE(hash-appendix-c, [hash])",
            "      | ^"
            );

        var args = new ClangTidy.Args(null, 1, false, ["libs"], false, false, false, []);
        var ret = await tidy.HandleRunClangTidyCommand(exec, vfs, paths, cwd, new CompileCommandsArguments(), log, false, args);
        using (new AssertionScope(log.Print()))
        {
            ret.Should().Be(-1);
            log.ErrorsAndWarnings.Should().BeEquivalentTo(
                "ERROR: C:\\executables\\clang-tidy.exe exited with -1",
                // todo(Gustav): remove this warning
                "WARNING: Invalid line: 'note: expanded from here'");
            log.RawMessages.Should().BeEquivalentTo(
                @"src\libs\foobar.cc (7/1) warning: function 'CATCH2_INTERNAL_TEST_0' declared 'static', move to anonymous namespace instead[misc-use-anonymous-namespace]",
                "    7 | TEST_CASE(hs-hash, [hash])",
                "      | ^",
                @"src\libs\foobar.hpp (144/28) note: expanded from macro 'TEST_CASE'",
                "  144 |   #define TEST_CASE( ... ) INTERNAL_CATCH_TESTCASE( __VA_ARGS__ )",
                "      |                            ^",
                @"src\libs\foobar.cc (13/1) warning: function 'CATCH2_INTERNAL_TEST_2' declared 'static', move to anonymous namespace instead[misc-use-anonymous-namespace]",
                "   13 | TEST_CASE(hash-appendix-c, [hash])",
                "      | ^");
        }
    }

    private void AddClangTidyResult(Fil clang_tidy, Dir cwd, Fil source, int exit, params string[] result)
    {
        exec.Add(new(clang_tidy, cwd, 2, source.Path, exit, result));
    }
}
