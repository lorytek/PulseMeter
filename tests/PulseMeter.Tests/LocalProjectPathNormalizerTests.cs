using PulseMeter.Shared.Projects;

namespace PulseMeter.Tests;

public sealed class LocalProjectPathNormalizerTests
{
    [Fact]
    public void Normalize_CollapsesBenchmarkCheckoutToItsOwningProject()
    {
        var path = @"\\?\C:\Projects\Headroom\.codex-benchmark-runs\strict-blind\checkouts\accesskit-accesskit";

        var normalized = LocalProjectPathNormalizer.Normalize(path);

        Assert.Equal(@"C:\Projects\Headroom", normalized);
        Assert.Equal("Headroom", LocalProjectPathNormalizer.GetDisplayName(path));
    }

    [Fact]
    public void Normalize_PreservesOrdinaryProjectPath()
    {
        var path = @"C:\Projects\PulseMeter";

        Assert.Equal(path, LocalProjectPathNormalizer.Normalize(path));
        Assert.Equal("PulseMeter", LocalProjectPathNormalizer.GetDisplayName(path));
    }

    [Fact]
    public void Normalize_MapsCodexWorktreeToItsOwningRepository()
    {
        var root = Path.Combine(Path.GetTempPath(), "PulseMeter.Tests", Guid.NewGuid().ToString("N"));
        var owner = Path.Combine(root, "Projects", "WPF");
        var ownerGitDirectory = Path.Combine(owner, ".git");
        var worktree = Path.Combine(root, ".codex", "worktrees", "4d0f", "WPF");
        var worktreeGitDirectory = Path.Combine(ownerGitDirectory, "worktrees", "WPF");
        Directory.CreateDirectory(worktree);
        Directory.CreateDirectory(worktreeGitDirectory);
        File.WriteAllText(Path.Combine(worktree, ".git"), $"gitdir: {worktreeGitDirectory}");
        File.WriteAllText(Path.Combine(worktreeGitDirectory, "commondir"), "../..");

        try
        {
            Assert.Equal(owner, LocalProjectPathNormalizer.Normalize(worktree));
            Assert.Equal("WPF", LocalProjectPathNormalizer.GetDisplayName(worktree));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Normalize_DoesNotMergeAnOrdinarySameNamedFolderWithoutWorktreeMetadata()
    {
        var path = @"C:\Projects\4d0f\WPF";

        Assert.Equal(path, LocalProjectPathNormalizer.Normalize(path));
    }

    [Fact]
    public void TryGetCodexWorktreeProjectName_RecognizesDeletedWorktreePath()
    {
        var path = @"C:\Users\tester\.codex\worktrees\0245\WPF";

        var recognized = LocalProjectPathNormalizer.TryGetCodexWorktreeProjectName(path, out var projectName);

        Assert.True(recognized);
        Assert.Equal("WPF", projectName);
        Assert.False(LocalProjectPathNormalizer.TryGetCodexWorktreeProjectName(@"C:\Projects\0245\WPF", out _));
    }

    [Fact]
    public void Normalize_PreservesExtendedUncProjectPath()
    {
        var path = @"\\?\UNC\server\share\PulseMeter";

        Assert.Equal(@"\\server\share\PulseMeter", LocalProjectPathNormalizer.Normalize(path));
        Assert.Equal("PulseMeter", LocalProjectPathNormalizer.GetDisplayName(path));
    }

    [Fact]
    public void Normalize_CollapsesNestedRunRepositoryToItsOwningProject()
    {
        var path = @"C:\Projects\Searchability\.runs\blind-real\repos\automapper-baseline";

        Assert.Equal(@"C:\Projects\Searchability", LocalProjectPathNormalizer.Normalize(path));
    }

    [Fact]
    public void IsUserProjectPath_RejectsTemporaryAndDiagnosticRoots()
    {
        var temporaryProject = Path.Combine(Path.GetTempPath(), "headroom-benchmark", "sample-repo");

        Assert.False(LocalProjectPathNormalizer.IsUserProjectPath(temporaryProject));
        Assert.False(LocalProjectPathNormalizer.IsUserProjectPath(@"C:\hrblind\run-1\sample-repo"));
        Assert.False(LocalProjectPathNormalizer.IsUserProjectPath(@"C:\hrdiag\run-1\sample-repo"));
        Assert.True(LocalProjectPathNormalizer.IsUserProjectPath(@"C:\Projects\PulseMeter"));
    }

    [Fact]
    public void MalformedPath_IsTreatedAsUnknownInsteadOfReachingPathApisAgain()
    {
        var malformed = "C:\\Projects\\bad\0path";

        Assert.Equal("(unknown project)", LocalProjectPathNormalizer.Normalize(malformed));
        Assert.Equal("Unknown project", LocalProjectPathNormalizer.GetDisplayName(malformed));
        Assert.False(LocalProjectPathNormalizer.IsUserProjectPath(malformed));
    }
}
