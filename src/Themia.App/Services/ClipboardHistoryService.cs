using Themia.Core.Diagnostics;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Services;

/// <summary>
/// [S-1458] The app's one clipboard history, which every Clipboard widget shows. It listens while the
/// state holds a Clipboard widget on any view (<see cref="ClipboardInterest"/>), not only while one
/// is on screen: a history kept by the widget itself started empty after every view switch and lost
/// whatever was copied while its view was away.
/// <para>
/// The rules of <see cref="ClipboardHistory"/> stand: text only, bounded, in memory for the session,
/// never written. When the last Clipboard widget goes the listener stops and the history is
/// forgotten. UI thread only, as <see cref="ClipboardWatcher"/> is.
/// </para>
/// </summary>
internal sealed class ClipboardHistoryService : IClipboardHistory, IDisposable
{
    private const string Scope = "clipboard";

    private readonly ClipboardHistory _history = new();
    private IDisposable? _watch;

    /// <summary>The history changed: an entry came, moved, went, or all of it was cleared.</summary>
    public event Action? Changed;

    /// <summary>Most recent first.</summary>
    public IReadOnlyList<ClipboardEntry> Entries => _history.Entries;

    /// <summary>The clipboard listener is running.</summary>
    public bool Listening => ClipboardWatcher.IsRunning;

    /// <summary>
    /// Follow the state: listen and keep <paramref name="capacity"/> entries, or with null (no
    /// Clipboard widget anywhere) stop listening and forget the history.
    /// </summary>
    public void SetWanted(int? capacity)
    {
        if (capacity is not { } keep)
        {
            if (_watch is null)
            {
                return;
            }

            _watch.Dispose();
            _watch = null;
            Clear();
            return;
        }

        var before = _history.Count;
        _history.Capacity = keep;
        _watch ??= ClipboardWatcher.Subscribe(OnText);

        if (_history.Count != before)
        {
            Raise();
        }
    }

    /// <summary>[S-1444] Forget one entry. Leaves the clipboard itself alone.</summary>
    public void Remove(string text)
    {
        if (_history.Remove(text))
        {
            Raise();
        }
    }

    /// <summary>Forget everything, for every Clipboard widget.</summary>
    public void Clear()
    {
        if (_history.Clear())
        {
            Raise();
        }
    }

    private void OnText(string text)
    {
        if (_history.Add(text, DateTimeOffset.Now))
        {
            Raise();
        }
    }

    private void Raise()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            // One widget that throws while redrawing must not stop the others hearing of the change.
            Log.Error(Scope, "a clipboard history listener threw", ex);
        }
    }

    public void Dispose()
    {
        _watch?.Dispose();
        _watch = null;
        _history.Clear();
    }
}
