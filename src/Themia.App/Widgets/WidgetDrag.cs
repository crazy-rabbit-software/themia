using Themia.App.Services.Files;
using Themia.Core.Diagnostics;
using Themia.Core.Files;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1022] One OLE drag over a desktop surface, from the moment it enters to its drop or its leaving:
/// what it carries (read once, on entry, so a move over a widget never goes back to the source), the
/// keys and the effects the source allows as they change, and — for a view that drops into a folder —
/// the forwarding to that folder's shell drop target.
/// <para>
/// The source's data object is held (AddRef) for the drag's life, because OLE hands it over only on
/// entry and on the drop while a shell target has to be entered again whenever the folder under the
/// pointer changes. The surface creates and disposes the drag; a view uses it only inside the
/// <see cref="IWidgetDropTarget"/> calls.
/// </para>
/// <para>UI thread only, like every OLE drop callback.</para>
/// </summary>
internal sealed class WidgetDrag : IWidgetDrag, IDisposable
{
    private const string Scope = "surface-drop";

    private DragDropInterop.ComRef _data;
    private DragDropInterop.ComRef _forward;
    private string? _forwardFolder;

    public WidgetDrag(nint data, uint keys, int screenX, int screenY, DropEffect allowed)
    {
        DragDropInterop.AddRef(data);
        _data = new DragDropInterop.ComRef(data);
        Update(keys, screenX, screenY, allowed);

        var paths = DragDropInterop.ReadPaths(data);

        // [S-1025] Chromium apps (VS Code) offer CF_HDROP and then fail to render it; the paths are
        // in their text as file:/// lines instead.
        Paths = paths.Count > 0 ? paths : SurfaceDrops.FileUrls(DragDropInterop.ReadText(data));
        VirtualFileNames = [.. DragDropInterop.ReadVirtualFiles(data).Select(v => v.File.RelativePath)];

        // A browser's link counts only when no file came with it (original S-34): a dragged image
        // carries both, and the file is what was meant.
        Urls = Paths.Count == 0 && VirtualFileNames.Count == 0 ? DragDropInterop.ReadUrls(data) : [];
        HasShellItems = DragDropInterop.HasShellItems(data);
    }

    /// <summary>
    /// The source's <c>IDataObject</c>, for what the summary does not carry (saving virtual files,
    /// reading the shell items a Start-menu drag offers). Valid inside the view calls; 0 once the drag ended.
    /// </summary>
    public nint Data => _data.Pointer;

    /// <summary>File-system paths (<c>CF_HDROP</c>, or a Chromium app's <c>file:///</c> lines), in the source's order.</summary>
    public IReadOnlyList<string> Paths { get; }

    /// <summary>Web addresses (<c>UniformResourceLocatorW</c>, or http(s) text), only read when no file came with the drag.</summary>
    public IReadOnlyList<string> Urls { get; }

    /// <summary>How many "virtual" files it offers (Outlook attachments).</summary>
    public int VirtualFiles => VirtualFileNames.Count;

    /// <summary>The virtual files' relative names.</summary>
    public IReadOnlyList<string> VirtualFileNames { get; }

    /// <summary>The drag carries shell items (<c>Shell IDList Array</c>): every Explorer drag, and a Start-menu app, which has nothing else.</summary>
    public bool HasShellItems { get; }

    /// <summary>
    /// How many things the drag carries, for logs: its paths, else its virtual files (Explorer offers
    /// both for the same files), else its links.
    /// </summary>
    public int ItemCount => Paths.Count > 0 ? Paths.Count : VirtualFiles > 0 ? VirtualFiles : Urls.Count;

    /// <summary>Whether the drag carries anything at all a widget or the desktop could take.</summary>
    public bool IsEmpty => Content == DropContent.None;

    public DropContent Content =>
        (Paths.Count > 0 ? DropContent.Files : DropContent.None)
        | (VirtualFiles > 0 ? DropContent.VirtualFiles : DropContent.None)
        | (Urls.Count > 0 ? DropContent.Urls : DropContent.None)
        | (HasShellItems ? DropContent.ShellItems : DropContent.None);

    /// <summary>The raw <c>MK_*</c> state of the last callback.</summary>
    public uint Keys { get; private set; }

    /// <summary>Ctrl held (copy rather than move, where that means something).</summary>
    public bool Ctrl => (Keys & SurfaceDrops.KeyControl) != 0;

    public bool Shift => (Keys & SurfaceDrops.KeyShift) != 0;

    /// <summary>The effects the source allows.</summary>
    public DropEffect Allowed { get; private set; }

    public int ScreenX { get; private set; }

    public int ScreenY { get; private set; }

    /// <summary>
    /// [S-1024] Where a drop would land, as the caption beside the drag image names it ("Move to
    /// <b>Downloads</b>"). A view sets it in <see cref="IWidgetDropTarget.DragOver"/>; the surface
    /// clears it before every call, and no place means no caption, just the cursor.
    /// </summary>
    public string? Place { get; set; }

    /// <summary>The surface's callbacks refresh the keys, the point and the allowed effects.</summary>
    public void Update(uint keys, int screenX, int screenY, DropEffect allowed)
    {
        Keys = keys;
        ScreenX = screenX;
        ScreenY = screenY;
        Allowed = allowed;
    }

    /// <summary>
    /// [S-1022] Hover over <paramref name="folder"/>: the effect its shell drop target answers, entered
    /// afresh when the folder changed (the previous one is left). <paramref name="forced"/> makes the
    /// shell do that effect (<see cref="SurfaceDrops.KeysFor"/>); None keeps the user's keys, so the
    /// shell's own defaults apply (move on one volume, copy across).
    /// </summary>
    public DropEffect ForwardOver(string folder, DropEffect forced)
    {
        var keys = SurfaceDrops.KeysFor(forced, Keys);
        var allowed = forced == DropEffect.None ? Allowed : Allowed & forced;

        if (allowed == DropEffect.None || _data.IsNull)
        {
            EndForward();
            return DropEffect.None;
        }

        if (_forwardFolder is null || !FileDrops.SamePath(_forwardFolder, folder))
        {
            EndForward();
            _forward = DragDropInterop.FolderDropTarget(folder);

            if (_forward.IsNull)
            {
                return DropEffect.None;
            }

            _forwardFolder = folder;
            return DragDropInterop.TargetEnter(_forward.Pointer, _data.Pointer, keys, ScreenX, ScreenY, allowed);
        }

        return DragDropInterop.TargetOver(_forward.Pointer, keys, ScreenX, ScreenY, allowed);
    }

    /// <summary>
    /// [S-1022] Drop into <paramref name="folder"/> through its shell drop target: the shell copies,
    /// moves or links, with its own conflict and progress UI and the source's move handshake. Returns
    /// what it did.
    /// </summary>
    public DropEffect ForwardDrop(string folder, DropEffect forced)
    {
        // Hover at this exact spot first: a drop without the hover before it is not something a shell
        // target is promised to handle, and a target that answers None would refuse the drop anyway.
        if (ForwardOver(folder, forced) == DropEffect.None || _forward.IsNull)
        {
            EndForward();
            return DropEffect.None;
        }

        var keys = SurfaceDrops.KeysFor(forced, Keys);
        var allowed = forced == DropEffect.None ? Allowed : Allowed & forced;
        var target = _forward.Detach();
        _forwardFolder = null;

        try
        {
            var done = DragDropInterop.TargetDrop(target, _data.Pointer, keys, ScreenX, ScreenY, allowed);
            Log.Debug(Scope, $"dropped {ItemCount} item(s) into the shell's target: {done}");
            return done;
        }
        finally
        {
            DragDropInterop.Release(target);
        }
    }

    /// <summary>Leave the folder target, if one is entered.</summary>
    public void EndForward()
    {
        if (!_forward.IsNull)
        {
            DragDropInterop.TargetLeave(_forward.Pointer);
            _forward.Dispose();
        }

        _forwardFolder = null;
    }

    /// <summary>
    /// Save the drag's virtual files under <paramref name="destination"/>, for a view that takes them
    /// itself rather than through a folder's shell target. Returns the paths written.
    /// </summary>
    public IReadOnlyList<string> SaveVirtualFiles(string destination)
        => _data.IsNull ? [] : DragDropInterop.SaveVirtualFiles(_data.Pointer, destination);

    public void Dispose()
    {
        EndForward();
        _data.Dispose();
    }
}
