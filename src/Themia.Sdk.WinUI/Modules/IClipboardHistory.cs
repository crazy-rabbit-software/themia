using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-1458] The app's one clipboard history as a Clipboard widget reaches it
/// (<see cref="IWidgetHost.ClipboardHistory"/>): kept by the host while any view holds a Clipboard
/// widget, fed by its clipboard listener, text only, in memory for the session (<see cref="ClipboardHistory"/>).
/// </summary>
public interface IClipboardHistory
{
    /// <summary>The history changed: an entry came, moved, went, or all of it was cleared. Raised on the UI thread.</summary>
    event Action? Changed;

    /// <summary>Most recent first.</summary>
    IReadOnlyList<ClipboardEntry> Entries { get; }

    /// <summary>The host's clipboard listener is running (else nothing copied can arrive).</summary>
    bool Listening { get; }

    /// <summary>[S-1444] Forget one entry. Leaves the clipboard itself alone.</summary>
    void Remove(string text);

    /// <summary>Forget everything, for every Clipboard widget.</summary>
    void Clear();
}
