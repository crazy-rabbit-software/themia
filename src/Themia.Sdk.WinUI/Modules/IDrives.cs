using Themia.App.Services;
using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-4655] The drives as a widget reaches them (<see cref="IWidgetHost.Drives"/>): the host's one walk
/// that every drive widget shares (the disk meter's volumes, Drive, Removable drives), made again
/// when Windows says a volume came or went and every 30 s of space while a widget watches. UI thread only.
/// </summary>
public interface IDrives
{
    /// <summary>The last walk; empty until the first one is in (<see cref="HasValue"/>).</summary>
    IReadOnlyList<DriveEntry> Drives { get; }

    bool HasValue { get; }

    /// <summary>Watch the drives; <paramref name="onChanged"/> runs on the UI thread after each walk that changed something. Dispose to let go.</summary>
    IDisposable Watch(Ticker ticker, Action onChanged);

    /// <summary>Walk again now (a Refresh command).</summary>
    void Refresh();
}
