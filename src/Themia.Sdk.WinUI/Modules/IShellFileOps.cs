using Themia.App.Services.Files;
using Themia.Core.Files;
using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-1330] [S-1331] File operations done by the shell exactly as Explorer does them
/// (<see cref="IWidgetHost.FileOps"/>): copy, move, rename and delete through <c>IFileOperation</c>,
/// with the shell's own progress, conflict and Recycle Bin dialogs and Explorer's undo, plus open,
/// Open with, Properties, Show in Explorer, a new folder or file, cloud pinning, a shortcut's target
/// and a drag out to another app. <b>Delete always recycles.</b> The COM, its worker thread and OLE
/// stay host code.
/// </summary>
public interface IShellFileOps
{
    /// <summary>[S-1333] Folders an operation changed (sources' parents and the destination). Raised on the host's worker thread.</summary>
    event Action<IReadOnlyList<string>>? Completed;

    /// <summary>Copy <paramref name="sources"/> into <paramref name="destination"/> (into their own folder they are named "- Copy").</summary>
    Task<FileOpOutcome> CopyAsync(IReadOnlyList<string> sources, string destination);

    /// <summary>Move <paramref name="sources"/> into <paramref name="destination"/>.</summary>
    Task<FileOpOutcome> MoveAsync(IReadOnlyList<string> sources, string destination);

    /// <summary>[S-1330] Send <paramref name="paths"/> to the Recycle Bin. Never a permanent delete.</summary>
    Task<FileOpOutcome> RecycleAsync(IReadOnlyList<string> paths);

    /// <summary>[S-1331] Rename one item; the name was validated by <see cref="FileNames.Validate"/>.</summary>
    Task<FileOpOutcome> RenameAsync(string path, string newName);

    /// <summary>[S-1331] A new folder "New folder" (or the next free name) in <paramref name="destination"/>: its path, or null.</summary>
    string? NewFolder(string destination);

    /// <summary>[S-1331] A new file from one of Explorer's New templates in <paramref name="destination"/>: its path, or null.</summary>
    string? NewFile(string destination, ShellNewTemplate template);

    /// <summary>[S-1330] Open an item as a double-click in Explorer would. Never throws.</summary>
    void Open(string path);

    /// <summary>[S-1330] Windows' "Open with" dialog for one file.</summary>
    void OpenWith(string path);

    /// <summary>[S-1330] The Properties sheet: one item's, or the shared sheet for several.</summary>
    void Properties(IReadOnlyList<string> paths);

    /// <summary>[S-1330] Open Explorer at the items' folder with them selected.</summary>
    void ShowInExplorer(IReadOnlyList<string> paths);

    /// <summary>[S-1324] "Always keep on this device" / "Free up space" on each path: how many took it.</summary>
    int SetCloudPin(IReadOnlyList<string> paths, CloudPin pin);

    /// <summary>A <c>.lnk</c>'s target (with its arguments unless <paramref name="withoutArguments"/>), or null.</summary>
    string? ResolveShortcut(string path, bool withoutArguments = false);

    /// <summary>Drag <paramref name="paths"/> out of <paramref name="window"/> to any app, in OLE's own loop: what the target did.</summary>
    DropEffect DragOut(IReadOnlyList<string> paths, DropEffect allowed, nint window);
}
