using System.Buffers.Binary;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PulseMeter.Tests;

public sealed class BrandingAssetTests
{
    [Fact]
    public void AppProject_EmbedsPulseMeterLogoAndExecutableIcon()
    {
        var project = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "PulseMeter.csproj"));

        Assert.Contains("<ApplicationIcon>Assets\\PulseMeter.ico</ApplicationIcon>", project);
        Assert.Contains("<Resource Include=\"Assets\\PulseMeterLogo.png\" />", project);
        Assert.Contains("<Resource Include=\"Assets\\PulseMeter.ico\" />", project);
    }

    [Fact]
    public void PulseMeterWindow_UsesPulseMeterWindowIcon()
    {
        var xaml = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "PulseMeterWindow", "UI", "PulseMeterWindow.xaml"));

        Assert.Contains("Icon=\"/Assets/PulseMeter.ico\"", xaml);
    }

    [Fact]
    public void ExpandedHeader_ShowsPulseMeterLogoBeforeTitle()
    {
        var xaml = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Slices", "ExpandedHeader", "UI", "ExpandedHeader.xaml"));
        var headerStart = xaml.IndexOf("x:Name=\"ExpandedStickyHeader\"", StringComparison.Ordinal);
        var controlsStart = xaml.IndexOf("Grid.Column=\"1\"", headerStart, StringComparison.Ordinal);

        Assert.NotEqual(-1, headerStart);
        Assert.True(controlsStart > headerStart);

        var headerTitleArea = xaml[headerStart..controlsStart];

        Assert.Contains("x:Name=\"ExpandedHeaderLogo\"", headerTitleArea);
        Assert.Contains("ImageSource=\"/Assets/PulseMeterLogo.png\"", headerTitleArea);
        Assert.True(
            headerTitleArea.IndexOf("x:Name=\"ExpandedHeaderLogo\"", StringComparison.Ordinal)
                < headerTitleArea.IndexOf("Text=\"{Binding CompactTitleText}\"", StringComparison.Ordinal));
    }

    [Fact]
    public void TrayIcon_LoadsPulseMeterIconResource()
    {
        var source = File.ReadAllText(FindWorkspaceFile("src", "PulseMeter", "Platform", "Windows", "TrayIconService.cs"));

        Assert.Contains("Assets/PulseMeter.ico", source);
        Assert.DoesNotContain("SystemIcons.Information", source);
    }

    [Fact]
    public void PulseMeterIcon_ContainsStandardWindowsIconSizes()
    {
        var entries = ReadIconEntries(FindWorkspaceFile("src", "PulseMeter", "Assets", "PulseMeter.ico"));
        var sizes = entries.Select(entry => entry.Width).Order().ToArray();

        Assert.Equal([16, 24, 32, 48, 64, 128, 256], sizes);
        Assert.All(entries, entry => Assert.Equal(entry.Width, entry.Height));
    }

    [Theory]
    [InlineData(32)]
    [InlineData(256)]
    public void PulseMeterIcon_ArtworkFillsTaskbarCanvas(int size)
    {
        var entries = ReadIconEntries(FindWorkspaceFile("src", "PulseMeter", "Assets", "PulseMeter.ico"));
        var entry = Assert.Single(entries.Where(entry => entry.Width == size));

        var coverage = MeasureVisibleBoundsCoverage(entry);

        Assert.True(
            coverage >= 0.55,
            $"Expected {size}x{size} icon artwork to fill at least 55% of its bounds, but it filled {coverage:P1}.");
    }

    private static string FindWorkspaceFile(params string[] segments)
    {
        return TestWorkspace.FindFile(segments);
    }

    private static IReadOnlyList<IconEntry> ReadIconEntries(string path)
    {
        var bytes = File.ReadAllBytes(path);

        Assert.True(bytes.Length >= 6, "ICO file is too small to contain a header.");
        Assert.Equal(0, ReadUInt16(bytes, 0));
        Assert.Equal(1, ReadUInt16(bytes, 2));

        var count = ReadUInt16(bytes, 4);
        var entries = new List<IconEntry>(count);

        for (var index = 0; index < count; index++)
        {
            var offset = 6 + index * 16;
            Assert.True(bytes.Length >= offset + 16, "ICO directory is truncated.");

            var width = bytes[offset] == 0 ? 256 : bytes[offset];
            var height = bytes[offset + 1] == 0 ? 256 : bytes[offset + 1];
            var dataLength = ReadInt32(bytes, offset + 8);
            var dataOffset = ReadInt32(bytes, offset + 12);

            Assert.True(dataLength > 0, "ICO image entry is empty.");
            Assert.True(dataOffset >= 0 && dataOffset + dataLength <= bytes.Length, "ICO image entry points outside the file.");

            var data = bytes.AsSpan(dataOffset, dataLength).ToArray();
            entries.Add(new IconEntry(width, height, data));
        }

        return entries;
    }

    private static double MeasureVisibleBoundsCoverage(IconEntry entry)
    {
        using var stream = new MemoryStream(entry.Data);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var bitmap = decoder.Frames[0];

        Assert.Equal(entry.Width, bitmap.PixelWidth);
        Assert.Equal(entry.Height, bitmap.PixelHeight);

        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);

        var minX = converted.PixelWidth;
        var minY = converted.PixelHeight;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < converted.PixelHeight; y++)
        {
            for (var x = 0; x < converted.PixelWidth; x++)
            {
                var alpha = pixels[y * stride + x * 4 + 3];
                if (alpha <= 16)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        Assert.True(maxX >= 0 && maxY >= 0, "ICO image entry has no visible pixels.");

        var visibleWidth = maxX - minX + 1;
        var visibleHeight = maxY - minY + 1;
        return (double)(visibleWidth * visibleHeight) / (converted.PixelWidth * converted.PixelHeight);
    }

    private static ushort ReadUInt16(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
    }

    private static int ReadInt32(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
    }

    private sealed record IconEntry(int Width, int Height, byte[] Data);
}
