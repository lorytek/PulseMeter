using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using PulseMeter.Slices.UsageCollection;

namespace PulseMeter.Platform.Windows;

public enum TrayConfidenceState
{
    Starting,
    Syncing,
    Live,
    Stale,
    Unavailable,
    Mock,
    Unknown
}

public static class TrayConfidenceBeacon
{
    public static TrayConfidenceState Map(bool isStarting, bool isRefreshing, SyncStatus syncStatus) =>
        isRefreshing ? TrayConfidenceState.Syncing :
        isStarting ? TrayConfidenceState.Starting :
        syncStatus switch
        {
            SyncStatus.Live => TrayConfidenceState.Live,
            SyncStatus.Stale => TrayConfidenceState.Stale,
            SyncStatus.Unavailable => TrayConfidenceState.Unavailable,
            SyncStatus.Mocked => TrayConfidenceState.Mock,
            _ => TrayConfidenceState.Unknown
        };

    public static string Tooltip(TrayConfidenceState state) => state switch
    {
        TrayConfidenceState.Starting => "PulseMeter — Starting",
        TrayConfidenceState.Syncing => "PulseMeter — Syncing",
        TrayConfidenceState.Live => "PulseMeter — Live",
        TrayConfidenceState.Stale => "PulseMeter — Stale",
        TrayConfidenceState.Unavailable => "PulseMeter — Unavailable",
        TrayConfidenceState.Mock => "PulseMeter — Mock",
        _ => "PulseMeter — Unknown"
    };

    public static string Tooltip(TrayIconPresentation presentation) =>
        presentation.RemainingPercent is int remainingPercent
            ? $"PulseMeter — Weekly {remainingPercent}% left"
            : Tooltip(presentation.ConfidenceState);

    public static Color DotColor(TrayConfidenceState state) => state switch
    {
        TrayConfidenceState.Starting or TrayConfidenceState.Syncing => Color.FromArgb(31, 115, 255),
        TrayConfidenceState.Live => Color.FromArgb(22, 163, 74),
        TrayConfidenceState.Stale => Color.FromArgb(217, 119, 6),
        TrayConfidenceState.Unavailable => Color.FromArgb(220, 38, 38),
        TrayConfidenceState.Mock => Color.FromArgb(126, 34, 206),
        _ => Color.FromArgb(100, 116, 139)
    };

    public static Color UsageBadgeColor(int remainingPercent) => remainingPercent switch
    {
        >= 50 => Color.FromArgb(22, 163, 74),
        >= 25 => Color.FromArgb(31, 115, 255),
        >= 10 => Color.FromArgb(217, 119, 6),
        _ => Color.FromArgb(220, 38, 38)
    };
}

public readonly record struct TrayIconPresentation(
    TrayConfidenceState ConfidenceState,
    int? RemainingPercent)
{
    public static TrayIconPresentation Create(
        TrayConfidenceState confidenceState,
        double? remainingPercent)
    {
        if (confidenceState != TrayConfidenceState.Live
            || remainingPercent is not double value
            || !double.IsFinite(value))
        {
            return new TrayIconPresentation(confidenceState, null);
        }

        var rounded = (int)Math.Round(
            Math.Clamp(value, 0, 100),
            MidpointRounding.AwayFromZero);
        return new TrayIconPresentation(confidenceState, rounded);
    }
}

public sealed class TrayConfidenceTransitionTracker
{
    private TrayIconPresentation? _applied;

    public bool ShouldApply(TrayConfidenceState state) =>
        ShouldApply(TrayIconPresentation.Create(state, null));

    public bool ShouldApply(TrayIconPresentation presentation) => _applied != presentation;

    public void MarkApplied(TrayConfidenceState state) =>
        MarkApplied(TrayIconPresentation.Create(state, null));

    public void MarkApplied(TrayIconPresentation presentation) => _applied = presentation;
}

public sealed class TrayConfidenceIconCache : IDisposable
{
    private readonly Icon _baseIcon;
    private readonly Func<Icon, TrayIconPresentation, Icon> _iconFactory;
    private readonly Dictionary<TrayIconPresentation, Icon> _icons = new();
    private bool _disposed;

    public TrayConfidenceIconCache(Icon baseIcon, Func<Icon, TrayConfidenceState, Icon>? iconFactory = null)
    {
        _baseIcon = baseIcon ?? throw new ArgumentNullException(nameof(baseIcon));
        _iconFactory = iconFactory is null
            ? Create
            : (icon, presentation) => iconFactory(icon, presentation.ConfidenceState);
    }
    public int CachedIconCount => _icons.Count;

    public Icon Get(TrayConfidenceState state) =>
        Get(TrayIconPresentation.Create(state, null));

    public Icon Get(TrayIconPresentation presentation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_icons.TryGetValue(presentation, out var icon))
        {
            icon = _iconFactory(_baseIcon, presentation);
            ValidateFactoryResult(icon);
            _icons.Add(presentation, icon);
        }
        return icon;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var icon in _icons.Values) icon.Dispose();
        _icons.Clear();
    }

    private void ValidateFactoryResult(Icon? icon)
    {
        if (icon is null)
        {
            throw new InvalidOperationException("The tray confidence icon factory returned no icon.");
        }

        if (ReferenceEquals(icon, _baseIcon))
        {
            throw new InvalidOperationException("The tray confidence icon factory must return an icon it owns.");
        }

        if (_icons.Values.Any(cachedIcon => ReferenceEquals(cachedIcon, icon)))
        {
            throw new InvalidOperationException("The tray confidence icon factory must return a distinct icon for each state.");
        }
    }

    private static Icon Create(Icon baseIcon, TrayIconPresentation presentation)
    {
        return presentation.RemainingPercent is int remainingPercent
            ? CreateUsageBadge(baseIcon, remainingPercent)
            : CreateConfidenceIcon(
                baseIcon,
                TrayConfidenceBeacon.DotColor(presentation.ConfidenceState));
    }

    private static Icon CreateConfidenceIcon(Icon baseIcon, Color color)
    {
        using var bitmap = baseIcon.ToBitmap();
        var size = Math.Max(5, Math.Min(bitmap.Width, bitmap.Height) / 3);
        var bounds = new Rectangle(bitmap.Width - size - 1, bitmap.Height - size - 1, size, size);
        using (var canvas = Graphics.FromImage(bitmap))
        using (var outline = new SolidBrush(Color.White))
        using (var fill = new SolidBrush(color))
        {
            canvas.SmoothingMode = SmoothingMode.AntiAlias;
            canvas.FillEllipse(outline, bounds.X - 1, bounds.Y - 1, bounds.Width + 2, bounds.Height + 2);
            canvas.FillEllipse(fill, bounds);
        }
        return CloneIcon(bitmap);
    }

    private static Icon CreateUsageBadge(Icon baseIcon, int remainingPercent)
    {
        var width = Math.Max(16, baseIcon.Width);
        var height = Math.Max(16, baseIcon.Height);
        using var bitmap = new Bitmap(width, height);
        using (var canvas = Graphics.FromImage(bitmap))
        using (var background = new SolidBrush(TrayConfidenceBeacon.UsageBadgeColor(remainingPercent)))
        using (var outline = new Pen(Color.FromArgb(72, 15, 23, 42), Math.Max(1, width / 32f)))
        using (var path = CreateRoundedRectangle(
                   new RectangleF(1, 1, width - 2, height - 2),
                   Math.Max(3, Math.Min(width, height) * 0.2f)))
        using (var textBrush = new SolidBrush(Color.White))
        using (var font = new Font(
                   "Segoe UI",
                   height * (remainingPercent >= 100 ? 0.36f : 0.48f),
                   FontStyle.Bold,
                   GraphicsUnit.Pixel))
        using (var format = new StringFormat
               {
                   Alignment = StringAlignment.Center,
                   LineAlignment = StringAlignment.Center,
                   FormatFlags = StringFormatFlags.NoWrap
               })
        {
            canvas.Clear(Color.Transparent);
            canvas.SmoothingMode = SmoothingMode.AntiAlias;
            canvas.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            canvas.FillPath(background, path);
            canvas.DrawPath(outline, path);
            canvas.DrawString(
                remainingPercent.ToString(System.Globalization.CultureInfo.InvariantCulture),
                font,
                textBrush,
                new RectangleF(0, -0.5f, width, height),
                format);
        }

        return CloneIcon(bitmap);
    }

    private static GraphicsPath CreateRoundedRectangle(RectangleF bounds, float radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Icon CloneIcon(Bitmap bitmap)
    {
        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
