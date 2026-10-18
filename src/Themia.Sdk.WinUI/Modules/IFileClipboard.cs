using Themia.App.Services.Files;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-1329] Copy, cut and paste of files on the real Windows clipboard, interchangeable with Explorer
/// (<see cref="IWidgetHost.FileClipboard"/>): the host's one, which remembers a cut so every folder
/// widget dims it while that clipboard content is still there. The OLE data object stays host code.
/// UI thread only.
/// </summary>
public interface IFileClipboard
{
    /// <summary>Raised (UI thread) when this app puts files on the clipboard or ends a cut, so views redraw dims and Paste.</summary>
    event Action? Changed;

    /// <summary>Whether the clipboard holds files (paths or virtual files) a paste could bring in. Cheap: no data is read.</summary>
    bool HasFiles { get; }

    /// <summary>Put <paramref name="paths"/> on the clipboard as a copy, or as a cut. Returns whether it landed.</summary>
    bool SetFiles(IReadOnlyList<string> paths, bool cut);

    /// <summary>[S-1329] Whether <paramref name="path"/> is cut and the cut is still what the clipboard holds.</summary>
    bool IsCut(string path);

    /// <summary>Whether a cut made here is still on the clipboard.</summary>
    bool CutStillPending();

    /// <summary>What is on the clipboard for a paste, or null when there are no files.</summary>
    FileClipboardContent? Read();

    /// <summary>[S-1332] Save the clipboard's virtual files (an attachment copied in Outlook) into <paramref name="destination"/>.</summary>
    IReadOnlyList<string> SaveVirtualFiles(string destination);

    /// <summary>A cut was pasted (as a move): the clipboard is emptied, as Explorer does, and the dims end.</summary>
    void CompleteCut();
}
