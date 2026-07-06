namespace PulseMeter.Tests;

public sealed class TestWorkspaceTests
{
    [Fact]
    public void FindFile_ResolvesFilesFromWorkspaceRoot()
    {
        var root = TestWorkspace.FindRoot();

        Assert.Equal(
            Path.Combine(root, "PulseMeter.slnx"),
            TestWorkspace.FindFile("PulseMeter.slnx"));
    }

    [Fact]
    public void FindRoot_UsesEnvironmentOverrideWhenPresent()
    {
        var previous = Environment.GetEnvironmentVariable("PULSEMETER_REPO_ROOT");
        try
        {
            var expected = TestWorkspace.FindRoot();
            Environment.SetEnvironmentVariable("PULSEMETER_REPO_ROOT", expected);

            Assert.Equal(expected, TestWorkspace.FindRoot());
        }
        finally
        {
            Environment.SetEnvironmentVariable("PULSEMETER_REPO_ROOT", previous);
        }
    }
}
