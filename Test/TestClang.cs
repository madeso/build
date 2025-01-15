using Workbench;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Workbench.Commands.Clang;
using Workbench.Shared;
using static Workbench.Commands.CheckIncludeOrder.CheckAction;

namespace Test;



public class TestClang : TestBase
{
    [Fact]
    public async Task if_clang_tidy_is_missing_then_fail()
    {
        var tidy = new ClangTidy();
        var cwd = new Dir(@"C:\test\");

        var args = new ClangTidy.Args(null, 1, false, ["libs"], true, false, false, []);
        var ret = await tidy.HandleRunClangTidyCommand(cwd, new CompileCommandsArguments(), log, false, args);
        using (new AssertionScope())
        {
            ret.Should().Be(-1);
            log.Errors.Should().Equal(["Failed to find valid clang-tidy executable"]);
        }
        /*
        read.AddContent(cwd.GetFile(Constants.ROOT_FILENAME_WITH_EXTENSION), "{}");

        var contentFolder = cwd.GetDir("content");
        var postFile = contentFolder.GetFile("test.md");

        var ret = await Facade.NewPost(run, read, write, postFile);

        using (new AssertionScope())
        {
            ret.Should().Be(0);
            run.Errors.Should().BeEmpty();
        }

        var content = write.GetContent(postFile);
        content.Should().EndWith("\n# Test");

        write.RemainingFiles.Should().BeEmpty();
        */
    }
}
