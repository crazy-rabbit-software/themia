using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-4841] [S-4843] Windows' Recent Items read into the files they point at
/// (<see cref="IWidgetHost.RecentFiles"/>): the host's one list for every Recent files widget, read
/// when the folder changes, never on a clock. Resolving the shortcuts stays host code. UI thread.
/// </summary>
public interface IRecentItems
{
    /// <summary>The list, newest first; null before the first read.</summary>
    IReadOnlyList<RecentFile>? Current { get; }

    /// <summary>The last read failed: the folder could not be listed.</summary>
    bool Unreadable { get; }

    /// <summary>Listen; <paramref name="changed"/> runs on the UI thread after each read. Dispose to stop.</summary>
    IDisposable Subscribe(Action changed);

    /// <summary>Read again (single-flight). Never throws.</summary>
    void Refresh();

    /// <summary>[S-4844] Take a file off the list as Explorer's "Remove from Recent" does; the file itself is untouched.</summary>
    void Remove(RecentFile file);
}
