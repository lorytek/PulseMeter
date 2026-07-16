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
}
