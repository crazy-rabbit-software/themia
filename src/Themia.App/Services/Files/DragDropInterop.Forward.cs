using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Core.Files;
using Themia.App.Interop;

namespace Themia.App.Services.Files;

/// <summary>
/// [S-1021]..[S-1025] The drop side's shell plumbing: a folder's own shell drop target (what Explorer
/// drops into), the drag-image helper that keeps the dragged thumbnail drawn over our windows, the
/// caption beside it, and the text a Chromium app offers instead of its files. Links are read in
/// <c>DragDropInterop.Urls.cs</c>.
/// <para>
/// Handing a drop to the folder's shell target rather than copying the files ourselves is what makes
/// a drop behave exactly as Explorer's: the optimized-move handshake with the source (so Explorer
/// never deletes what was moved, or deletes before the move ran), virtual files, links, conflict
/// and progress UI, undo in Explorer, and a right-button drag's "Move here / Copy here" menu.
/// </para>
/// </summary>
internal static unsafe partial class DragDropInterop
{

    private static readonly Guid BhidSfuiObject = new("3981E225-F559-11D3-8E3A-00C04F6837D5");
    private static readonly Guid ClsidDragDropHelper = new("4657278A-411B-11D2-839A-00C04FD918D0");
    private static readonly Guid IidDropTargetHelper = new("4657278B-411B-11D2-839A-00C04FD918D0");

    private static ushort _description;
    private static ushort _shellIdList;

    private static ushort ShellIdListFormat => _shellIdList != 0 ? _shellIdList : _shellIdList = Register("Shell IDList Array");

    private static ushort DescriptionFormat => _description != 0 ? _description : _description = Register("DropDescription");

    // ---- reading text --------------------------------------------------------------------------

    /// <summary>The data object's Unicode text, or null when it offers none.</summary>
    public static string? ReadText(nint data)
        => GetBytes(data, CfUnicodeText) is { } bytes ? SurfaceDrops.DecodeText(bytes) : null;

    /// <summary>
    /// Whether the data object offers shell items (<c>Shell IDList Array</c>), asked without rendering
    /// them (<c>IDataObject::QueryGetData</c>, slot 5).
    /// </summary>
    public static bool HasShellItems(nint data)
    {
        if (data == 0)
        {
            return false;
        }

        var formatEtc = new FormatEtc { Format = ShellIdListFormat, Aspect = DvAspectContent, Index = -1, Tymed = TymedHGlobal };
        var query = (delegate* unmanaged[Stdcall]<nint, FormatEtc*, int>)ComApi.Slot(data, 5);
        return query(data, &formatEtc) == SOk;
    }

    // ---- a folder's shell drop target ----------------------------------------------------------

    /// <summary>
    /// [S-1022] The shell's own <c>IDropTarget</c> for <paramref name="folder"/>, the object Explorer
    /// drops into when the pointer is over that folder. A null ref (logged) when the shell has none.
    /// </summary>
    public static ComPtr FolderDropTarget(string folder)
    {
        using var item = CreateItem(folder);

        if (item.IsNull)
        {
            return default;
        }

        nint target;
        var bhid = BhidSfuiObject;
        var iid = IidDropTarget;

        // IShellItem::BindToHandler is slot 3.
        var bind = (delegate* unmanaged[Stdcall]<nint, nint, Guid*, Guid*, nint*, int>)ComApi.Slot(item.Pointer, 3);
        var hr = bind(item.Pointer, 0, &bhid, &iid, &target);

        if (hr < 0 || target == 0)
        {
            Log.Warn(Scope, $"the shell has no drop target for '{folder}' (0x{hr:X8})");
            return default;
        }

        return new ComPtr(target);
    }

    /// <summary><c>IDropTarget::DragEnter</c> on a target we hold (slot 3). Returns the effect it answered.</summary>
    public static DropEffect TargetEnter(nint target, nint data, uint keys, int x, int y, DropEffect allowed)
    {
        var effect = (uint)allowed;
        var call = (delegate* unmanaged[Stdcall]<nint, nint, uint, PointL, uint*, int>)ComApi.Slot(target, 3);
        return call(target, data, keys, new PointL { X = x, Y = y }, &effect) < 0 ? DropEffect.None : (DropEffect)effect;
    }

    /// <summary><c>IDropTarget::DragOver</c> (slot 4).</summary>
    public static DropEffect TargetOver(nint target, uint keys, int x, int y, DropEffect allowed)
    {
        var effect = (uint)allowed;
        var call = (delegate* unmanaged[Stdcall]<nint, uint, PointL, uint*, int>)ComApi.Slot(target, 4);
        return call(target, keys, new PointL { X = x, Y = y }, &effect) < 0 ? DropEffect.None : (DropEffect)effect;
    }

    /// <summary><c>IDropTarget::DragLeave</c> (slot 5).</summary>
    public static void TargetLeave(nint target)
    {
        var call = (delegate* unmanaged[Stdcall]<nint, int>)ComApi.Slot(target, 5);
        _ = call(target);
    }

    /// <summary><c>IDropTarget::Drop</c> (slot 6). Returns the effect the target performed.</summary>
    public static DropEffect TargetDrop(nint target, nint data, uint keys, int x, int y, DropEffect allowed)
    {
        var effect = (uint)allowed;
        var call = (delegate* unmanaged[Stdcall]<nint, nint, uint, PointL, uint*, int>)ComApi.Slot(target, 6);
        var hr = call(target, data, keys, new PointL { X = x, Y = y }, &effect);

        if (hr < 0)
        {
            Log.Warn(Scope, $"the shell's drop failed (0x{hr:X8})");
            return DropEffect.None;
        }

        return (DropEffect)effect;
    }

    // ---- the drag image and its caption --------------------------------------------------------

    /// <summary>
    /// [S-1024] The shell's drag-image helper (<c>IDropTargetHelper</c>), which draws the source's
    /// thumbnail and caption over a target window. A null ref (logged) when it cannot be made; the
    /// drag then still works, with the plain cursor.
    /// </summary>
    public static ComPtr CreateDropHelper()
    {
        nint helper;
        var clsid = ClsidDragDropHelper;
        var iid = IidDropTargetHelper;
        var hr = ComApi.Create(clsid, iid, ComApi.ClsCtxInprocServer, out helper);

        if (hr < 0 || helper == 0)
        {
            Log.Warn(Scope, $"no drag-image helper (0x{hr:X8}); drags show the plain cursor");
            return default;
        }

        return new ComPtr(helper);
    }

    /// <summary><c>IDropTargetHelper::DragEnter</c> (slot 3).</summary>
    public static void HelperEnter(nint helper, nint window, nint data, int x, int y, DropEffect effect)
    {
        var point = new PointL { X = x, Y = y };
        var call = (delegate* unmanaged[Stdcall]<nint, nint, nint, PointL*, uint, int>)ComApi.Slot(helper, 3);
        _ = call(helper, window, data, &point, (uint)effect);
    }

    /// <summary><c>IDropTargetHelper::DragLeave</c> (slot 4).</summary>
    public static void HelperLeave(nint helper)
    {
        var call = (delegate* unmanaged[Stdcall]<nint, int>)ComApi.Slot(helper, 4);
        _ = call(helper);
    }

    /// <summary><c>IDropTargetHelper::DragOver</c> (slot 5).</summary>
    public static void HelperOver(nint helper, int x, int y, DropEffect effect)
    {
        var point = new PointL { X = x, Y = y };
        var call = (delegate* unmanaged[Stdcall]<nint, PointL*, uint, int>)ComApi.Slot(helper, 5);
        _ = call(helper, &point, (uint)effect);
    }

    /// <summary><c>IDropTargetHelper::Drop</c> (slot 6).</summary>
    public static void HelperDrop(nint helper, nint data, int x, int y, DropEffect effect)
    {
        var point = new PointL { X = x, Y = y };
        var call = (delegate* unmanaged[Stdcall]<nint, nint, PointL*, uint, int>)ComApi.Slot(helper, 6);
        _ = call(helper, data, &point, (uint)effect);
    }

    /// <summary>
    /// [S-1024] Put the caption beside the drag image ("Move to <b>Desktop</b>"): a
    /// <c>DropDescription</c> on the source's data object, which the source's drag-image window draws.
    /// <see cref="DropImage.Invalid"/> hands the cursor back to the source. A source that does not
    /// take descriptions just keeps its cursor — not an error.
    /// </summary>
    public static void SetDescription(nint data, DropFeedback feedback)
        => SetBytes(data, DescriptionFormat, SurfaceDrops.EncodeDescription(feedback.Image, feedback.Message, feedback.Insert), quiet: true);

}
