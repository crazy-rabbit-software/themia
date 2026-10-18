using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Core.Files;

namespace Themia.App.Services.Files;

/// <summary>
/// [S-1329] Copy, cut and paste of files on the real Windows clipboard, interchangeable with
/// Explorer: Ctrl+C in a folder widget and Ctrl+V in Explorer works, and the other way round.
/// <para>
/// Writing puts the shell's own data object (<see cref="DragDropInterop.CreateDataObject"/>: CF_HDROP,
/// shell ID lists, <c>Preferred DropEffect</c> = Move for a cut) on the clipboard and flushes it at
/// once, so the clipboard holds plain data that outlives this process and nobody ever calls back into
/// this app to paste it. Reading takes the clipboard's data object and reads the paths, the effect and
/// any virtual files out of it.
/// </para>
/// <para>
/// A cut is remembered with the clipboard's sequence number, so the cut items are dimmed exactly
/// while that clipboard content is still there ([S-1329]): another copy anywhere, or a paste that
/// empties it, changes the number and the dimming ends. UI thread only.
/// </para>
/// </summary>
internal static unsafe partial class FileClipboard
{
    private const string Scope = "file-clipboard";
    private static readonly HashSet<string> Cut = new(StringComparer.OrdinalIgnoreCase);
    private static uint _cutSequence;

    /// <summary>Raised (UI thread) when this app puts files on the clipboard or ends a cut, so views redraw dims and Paste.</summary>
    public static event Action? Changed;

    /// <summary>Put <paramref name="paths"/> on the clipboard as a copy, or as a cut. Returns whether it landed.</summary>
    public static bool SetFiles(IReadOnlyList<string> paths, bool cut)
    {
        ArgumentNullException.ThrowIfNull(paths);

        if (paths.Count == 0 || !DragDropInterop.EnsureOle())
        {
            return false;
        }

        using var data = DragDropInterop.CreateDataObject(paths, cut ? DropEffect.Move : DropEffect.Copy);

        if (data.IsNull)
        {
            return false;
        }

        var hr = OleSetClipboard(data.Pointer);

        if (hr < 0)
        {
            // Another application holding the clipboard open. Said, not retried on a timer.
            Log.Warn(Scope, $"OleSetClipboard failed (0x{hr:X8})");
            return false;
        }

        hr = OleFlushClipboard();

        if (hr < 0)
        {
            Log.Warn(Scope, $"OleFlushClipboard failed (0x{hr:X8}); the files stay on the clipboard while Themia runs");
        }

        Cut.Clear();

        if (cut)
        {
            Cut.UnionWith(paths);
        }

        _cutSequence = GetClipboardSequenceNumber();
        Raise();
        return true;
    }

    /// <summary>Whether the clipboard holds files (paths or virtual files) a paste could bring in. Cheap: no data is read.</summary>
    public static bool HasFiles
        => IsClipboardFormatAvailable(DragDropInterop.CfHdrop) || IsClipboardFormatAvailable(DragDropInterop.FileDescriptorFormat);

    /// <summary>[S-1329] Whether <paramref name="path"/> is cut and the cut is still what the clipboard holds.</summary>
    public static bool IsCut(string path) => Cut.Count > 0 && CutStillPending() && Cut.Contains(path);

    /// <summary>Whether a cut made here is still on the clipboard.</summary>
    public static bool CutStillPending()
    {
        if (Cut.Count == 0)
        {
            return false;
        }

        if (GetClipboardSequenceNumber() == _cutSequence)
        {
            return true;
        }

        Cut.Clear();
        return false;
    }

    /// <summary>What is on the clipboard for a paste, or null when there are no files.</summary>
    public static FileClipboardContent? Read()
    {
        if (!HasFiles || !DragDropInterop.EnsureOle())
        {
            return null;
        }

        nint data;
        var hr = OleGetClipboard(&data);

        if (hr < 0 || data == 0)
        {
            Log.Warn(Scope, $"OleGetClipboard failed (0x{hr:X8})");
            return null;
        }

        using var owned = new DragDropInterop.ComRef(data);
        var paths = DragDropInterop.ReadPaths(data);
        var effect = DragDropInterop.ReadPreferredEffect(data);
        var virtualFiles = paths.Count == 0 ? DragDropInterop.ReadVirtualFiles(data).Count : 0;

        return new FileClipboardContent(paths, effect, virtualFiles);
    }

    /// <summary>[S-1332] Save the clipboard's virtual files (an attachment copied in Outlook) into <paramref name="destination"/>.</summary>
    public static IReadOnlyList<string> SaveVirtualFiles(string destination)
    {
        if (!DragDropInterop.EnsureOle())
        {
            return [];
        }

        nint data;

        if (OleGetClipboard(&data) < 0 || data == 0)
        {
            return [];
        }

        using var owned = new DragDropInterop.ComRef(data);
        return DragDropInterop.SaveVirtualFiles(data, destination);
    }

    /// <summary>
    /// A cut was pasted (here, as a move): Explorer empties the clipboard after a cut-paste, so this
    /// does too, and the dims end.
    /// </summary>
    public static void CompleteCut()
    {
        if (Cut.Count == 0)
        {
            return;
        }

        Cut.Clear();

        if (DragDropInterop.EnsureOle())
        {
            var hr = OleSetClipboard(0);

            if (hr < 0)
            {
                Log.Warn(Scope, $"could not clear the clipboard after a cut-paste (0x{hr:X8})");
            }
        }

        Raise();
    }

    private static void Raise()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a clipboard listener failed", ex);
        }
    }

    [LibraryImport("ole32.dll")]
    private static partial int OleGetClipboard(nint* data);

    [LibraryImport("ole32.dll")]
    private static partial int OleSetClipboard(nint data);

    [LibraryImport("ole32.dll")]
    private static partial int OleFlushClipboard();

    [LibraryImport("user32.dll")]
    private static partial uint GetClipboardSequenceNumber();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsClipboardFormatAvailable(uint format);
}
