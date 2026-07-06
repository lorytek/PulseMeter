namespace PulseMeter.Tests;

public sealed class ArchitectureSliceLayoutTests
{
    private static readonly string OldAppNamespace = string.Concat("PulseMeter", ".", "App");
    private static readonly string OldCoreNamespace = string.Concat("PulseMeter", ".", "Core");
    private static readonly string OldFolderName = string.Concat("Feat", "ures");
    private static readonly string OldShellName = string.Concat("Sh", "ell");
    private static readonly string OldWindowContainerName = string.Concat("Meter", "Shell");

    [Fact]
    public void Solution_ContainsOnlySinglePulseMeterSourceProject()
    {
        var root = TestWorkspace.FindRoot();
        var solution = File.ReadAllText(Path.Combine(root, "PulseMeter.slnx"));

        Assert.Contains(@"src/PulseMeter/PulseMeter.csproj", solution);
        Assert.DoesNotContain($"src/{OldAppNamespace}/", solution);
        Assert.DoesNotContain($"src/{OldCoreNamespace}/", solution);
    }

    [Fact]
    public void TestProject_ReferencesPulseMeterProjectDirectly()
    {
        var root = TestWorkspace.FindRoot();
        var project = File.ReadAllText(Path.Combine(root, "tests", "PulseMeter.Tests", "PulseMeter.Tests.csproj"));

        Assert.Contains(@"..\..\src\PulseMeter\PulseMeter.csproj", project);
        Assert.DoesNotContain($@"..\..\src\{OldAppNamespace}\", project);
        Assert.DoesNotContain($@"..\..\src\{OldCoreNamespace}\", project);
        Assert.DoesNotContain("<Compile Include=", project);
    }

    [Fact]
    public void SourceRoot_UsesSingleProjectSliceFolders()
    {
        var root = TestWorkspace.FindRoot();
        var sourceRoot = Path.Combine(root, "src", "PulseMeter");

        Assert.True(Directory.Exists(sourceRoot), $"Expected source root at {sourceRoot}.");

        var allowedTopLevel = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Assets",
            "Bootstrap",
            "Platform",
            "Properties",
            "Shared",
            "Slices",
            "PulseMeter.csproj"
        };

        var unexpectedEntries = Directory.EnumerateFileSystemEntries(sourceRoot)
            .Where(path => !IsGeneratedOrBuildOutput(path))
            .Where(path => !allowedTopLevel.Contains(Path.GetFileName(path)))
            .Select(path => Path.GetRelativePath(sourceRoot, path))
            .OrderBy(path => path)
            .ToArray();

        Assert.Empty(unexpectedEntries);
    }

    [Fact]
    public void Slices_UseOnlyModelsBusinessAndUiSubfolders()
    {
        var root = TestWorkspace.FindRoot();
        var slicesRoot = Path.Combine(root, "src", "PulseMeter", "Slices");

        var expectedSlices = new[]
        {
            "AccountUsage",
            "DailyUsage",
            "DataBar",
            "ExpandedHeader",
            "NavigationRail",
            "ProjectUsage",
            "PulseMeterWindow",
            "RateLimits",
            "RateLimitsDaily",
            "ResetCredits",
            "UsageCollection"
        };

        foreach (var expectedSlice in expectedSlices)
        {
            Assert.True(Directory.Exists(Path.Combine(slicesRoot, expectedSlice)), $"Missing slice {expectedSlice}.");
        }

        var allowedSliceFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Models",
            "Business",
            "UI"
        };

        var unexpectedSliceFolders = Directory.EnumerateDirectories(slicesRoot)
            .SelectMany(slice => Directory.EnumerateDirectories(slice)
                .Where(folder => !allowedSliceFolders.Contains(Path.GetFileName(folder)))
                .Select(folder => Path.GetRelativePath(slicesRoot, folder)))
            .OrderBy(path => path)
            .ToArray();

        Assert.Empty(unexpectedSliceFolders);
    }

    [Fact]
    public void SliceRegistrations_UseRegistrationFileNames()
    {
        var root = TestWorkspace.FindRoot();
        var slicesRoot = Path.Combine(root, "src", "PulseMeter", "Slices");

        var oldRegistrationNames = Directory.EnumerateFiles(slicesRoot, "*ServiceCollectionExtensions.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedOrBuildOutput(path))
            .Select(path => Path.GetRelativePath(slicesRoot, path))
            .OrderBy(path => path)
            .ToArray();

        Assert.Empty(oldRegistrationNames);
    }

    [Fact]
    public void SliceSourceNamespaces_MirrorModelsBusinessAndUiFolders()
    {
        var root = TestWorkspace.FindRoot();
        var slicesRoot = Path.Combine(root, "src", "PulseMeter", "Slices");

        var violations = Directory.EnumerateFiles(slicesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedOrBuildOutput(path))
            .Select(path => new
            {
                Path = path,
                Parts = Path.GetRelativePath(slicesRoot, path)
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            })
            .Where(file => file.Parts.Length >= 3 && IsSliceLayer(file.Parts[1]))
            .Select(file => new
            {
                RelativePath = Path.GetRelativePath(root, file.Path),
                ExpectedNamespace = $"namespace PulseMeter.Slices.{file.Parts[0]}.{file.Parts[1]};",
                Text = File.ReadAllText(file.Path)
            })
            .Where(file => !file.Text.Contains(file.ExpectedNamespace, StringComparison.Ordinal))
            .Select(file => $"{file.RelativePath}: expected {file.ExpectedNamespace}")
            .OrderBy(violation => violation)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void SliceXamlClasses_MirrorUiNamespaces()
    {
        var root = TestWorkspace.FindRoot();
        var slicesRoot = Path.Combine(root, "src", "PulseMeter", "Slices");

        var violations = Directory.EnumerateFiles(slicesRoot, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedOrBuildOutput(path))
            .Select(path => new
            {
                Path = path,
                Parts = Path.GetRelativePath(slicesRoot, path)
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            })
            .Where(file => file.Parts.Length >= 3 && file.Parts[1].Equals("UI", StringComparison.OrdinalIgnoreCase))
            .Select(file => new
            {
                RelativePath = Path.GetRelativePath(root, file.Path),
                ExpectedClass = $"x:Class=\"PulseMeter.Slices.{file.Parts[0]}.UI.{Path.GetFileNameWithoutExtension(file.Path)}\"",
                Text = File.ReadAllText(file.Path)
            })
            .Where(file => !file.Text.Contains(file.ExpectedClass, StringComparison.Ordinal))
            .Select(file => $"{file.RelativePath}: expected {file.ExpectedClass}")
            .OrderBy(violation => violation)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Bootstrap_SeparatesStartupFromComposition()
    {
        var root = TestWorkspace.FindRoot();
        var bootstrapRoot = Path.Combine(root, "src", "PulseMeter", "Bootstrap");

        Assert.True(File.Exists(Path.Combine(bootstrapRoot, "Startup", "App.xaml")));
        Assert.True(File.Exists(Path.Combine(bootstrapRoot, "Startup", "App.xaml.cs")));
        Assert.True(File.Exists(Path.Combine(bootstrapRoot, "Startup", "PulseMeterApplication.cs")));
        Assert.True(File.Exists(Path.Combine(bootstrapRoot, "Composition", "PulseMeterCompositionRoot.cs")));
        Assert.True(File.Exists(Path.Combine(bootstrapRoot, "Composition", "PlatformRegistration.cs")));
        Assert.True(File.Exists(Path.Combine(bootstrapRoot, "Composition", "UsageCollectionRegistration.cs")));
        Assert.True(File.Exists(Path.Combine(bootstrapRoot, "Composition", "PulseMeterWindowRegistration.cs")));

        var looseBootstrapFiles = Directory.EnumerateFiles(bootstrapRoot)
            .Where(path => !IsGeneratedOrBuildOutput(path))
            .Select(path => Path.GetRelativePath(bootstrapRoot, path))
            .OrderBy(path => path)
            .ToArray();

        Assert.Empty(looseBootstrapFiles);
    }

    [Fact]
    public void ProductionSource_DoesNotUseOldArchitectureNames()
    {
        var root = TestWorkspace.FindRoot();
        var sourceRoot = Path.Combine(root, "src", "PulseMeter");
        var oldTextTokens = new[] { OldAppNamespace, OldCoreNamespace, OldWindowContainerName };

        var sourceFiles = Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedOrBuildOutput(path))
            .Where(path => Path.GetExtension(path) is ".cs" or ".xaml" or ".csproj")
            .ToArray();

        var pathViolations = sourceFiles
            .Select(path => Path.GetRelativePath(root, path))
            .Where(path => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part.Equals(OldFolderName, StringComparison.OrdinalIgnoreCase)
                    || part.Equals(OldShellName, StringComparison.OrdinalIgnoreCase)
                    || part.Contains(OldWindowContainerName, StringComparison.OrdinalIgnoreCase)))
            .Select(path => $"{path}: path")
            .ToArray();

        var textViolations = sourceFiles
            .SelectMany(path =>
            {
                var relativePath = Path.GetRelativePath(root, path);
                var text = File.ReadAllText(path);
                return oldTextTokens
                    .Where(token => text.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{relativePath}: {token}");
            })
            .Concat(pathViolations)
            .OrderBy(violation => violation)
            .ToArray();

        Assert.Empty(textViolations);
    }

    private static bool IsGeneratedOrBuildOutput(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return parts.Any(part => part.Equals("bin", StringComparison.OrdinalIgnoreCase)
            || part.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSliceLayer(string value)
    {
        return value is "Models" or "Business" or "UI";
    }
}
