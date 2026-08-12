using System.Drawing;
using System.Drawing.Drawing2D;
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

    public static Color DotColor(TrayConfidenceState state) => state switch
    {
        TrayConfidenceState.Starting or TrayConfidenceState.Syncing => Color.FromArgb(31, 115, 255),
        TrayConfidenceState.Live => Color.FromArgb(22, 163, 74),
        TrayConfidenceState.Stale => Color.FromArgb(217, 119, 6),
        TrayConfidenceState.Unavailable => Color.FromArgb(220, 38, 38),
        TrayConfidenceState.Mock => Color.FromArgb(126, 34, 206),
        _ => Color.FromArgb(100, 116, 139)
    };
}

public sealed class TrayConfidenceTransitionTracker
{
    private TrayConfidenceState? _applied;

    public bool ShouldApply(TrayConfidenceState state) => _applied != state;

    public void MarkApplied(TrayConfidenceState state) => _applied = state;
}

public sealed class TrayConfidenceIconCache : IDisposable
{
    private readonly Icon _baseIcon;
    private readonly Func<Icon, TrayConfidenceState, Icon> _iconFactory;
    private readonly Dictionary<TrayConfidenceState, Icon> _icons = new();
    private bool _disposed;

    public TrayConfidenceIconCache(Icon baseIcon, Func<Icon, TrayConfidenceState, Icon>? iconFactory = null)
    {
        _baseIcon = baseIcon ?? throw new ArgumentNullException(nameof(baseIcon));
        _iconFactory = iconFactory ?? ((icon, state) => Create(icon, TrayConfidenceBeacon.DotColor(state)));
    }
    public int CachedIconCount => _icons.Count;

    public Icon Get(TrayConfidenceState state)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_icons.TryGetValue(state, out var icon))
        {
            icon = _iconFactory(_baseIcon, state);
            ValidateFactoryResult(icon);
            _icons.Add(state, icon);
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

    private static Icon Create(Icon baseIcon, Color color)
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
