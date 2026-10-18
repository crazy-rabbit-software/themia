namespace Themia.Core.Model;

/// <summary>
/// [S-1458] A widget whose config asks the host to keep the clipboard history, and how many entries:
/// the host's <c>ClipboardInterest</c> asks the configs rather than naming a widget.
/// </summary>
public interface IKeepsClipboardHistory
{
    /// <summary>The entries this widget shows, already clamped to the history's bounds.</summary>
    int KeptEntries { get; }
}
