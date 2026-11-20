using Themia.Core.Diagnostics;

namespace Themia.App.Interop;

/// <summary>
/// [S-938] The desktop's folder view, reached the documented way: <c>IShellWindows</c> finds the
/// desktop's browser, the browser hands over its active <c>IShellView</c>, and the view is asked to
/// change its own selection.
/// <para>
/// The list view underneath could be told directly with <c>LVM_SETITEMSTATE</c>, but that message
/// carries a pointer, so the item would have to be written into Explorer's memory with
/// <c>VirtualAllocEx</c>/<c>WriteProcessMemory</c> — exactly the pattern security software flags as
/// code injection, and nothing a Store-signed desktop widget should do. The shell's own interfaces
/// marshal across the process boundary for us.
/// </para>
/// <para>
/// Function-pointer vtables, as in the Core Audio interop, so it stays Native AOT clean: no
/// built-in COM interop, no reflection. Each slot number is the interface's declaration order.
/// </para>
/// </summary>
internal static unsafe class DesktopFolderView
{
    private const string Scope = "desktop-icons";

    private const int SlotFindWindowSw = 15; // IShellWindows: IDispatch (7) + get_Count .. OnActivated (8)
    private const int SlotQueryService = 3; // IServiceProvider
    private const int SlotQueryActiveShellView = 15; // IShellBrowser: IOleWindow (5) + InsertMenusSB .. SendControlMsg (10)
    private const int SlotSelectItem = 14; // IShellView: IOleWindow (5) + TranslateAccelerator .. SaveViewState (9)

    private const ushort VtI4 = 3;
    private const int CsidlDesktop = 0;
    private const int SwcDesktop = 8;
    private const int SwfoNeedDispatch = 1;
    private const uint SvsiDeselectOthers = 0x4;

    private static readonly Guid ClsidShellWindows = new("9BA05972-F6A8-11CF-A442-00A0C90A8F39");
    private static readonly Guid IidShellWindows = new("85CB6900-4D95-11CF-960C-0080C7F4EE85");
    private static readonly Guid IidServiceProvider = new("6D5140C1-7436-11CE-8034-00AA006009FA");
    private static readonly Guid SidTopLevelBrowser = new("4C96BE40-915C-11CF-99D3-00AA004AE837");
    private static readonly Guid IidShellBrowser = new("000214E2-0000-0000-C000-000000000046");

    /// <summary>
    /// Deselect every icon on the desktop. Blocking (a cross-process call into Explorer): call it off
    /// the UI thread. Failures are logged at debug level — a selection left lit is cosmetic.
    /// </summary>
    public static void DeselectAll()
    {
        using var apartment = ComApi.Apartment.Enter(ComApi.MultiThreaded);
        if (!apartment.Usable)
        {
            Log.Debug(Scope, $"CoInitializeEx failed (0x{apartment.Result:X8}); desktop selection left as it is");
            return;
        }

        try
        {
            var hr = ComApi.Create(ClsidShellWindows, IidShellWindows, ComApi.ClsCtxLocalServer, out var made);
            using var windows = new ComPtr(made);
            if (hr < 0 || windows.IsNull)
            {
                Log.Debug(Scope, $"no shell windows (0x{hr:X8})");
                return;
            }

            var location = new Win32.Variant { Vt = VtI4, Value = CsidlDesktop };
            var root = default(Win32.Variant);
            int desktopHwnd;
            nint found = 0;
            hr = ((delegate* unmanaged[Stdcall]<nint, Win32.Variant*, Win32.Variant*, int, int*, int, nint*, int>)ComApi.Slot(windows.Pointer, SlotFindWindowSw))(
                windows.Pointer, &location, &root, SwcDesktop, &desktopHwnd, SwfoNeedDispatch, &found);
            using var dispatch = new ComPtr(found);
            if (hr < 0 || dispatch.IsNull)
            {
                Log.Debug(Scope, $"the desktop's browser was not found (0x{hr:X8})");
                return;
            }

            using var provider = new ComPtr(ComApi.Query(dispatch.Pointer, IidServiceProvider));
            if (provider.IsNull)
            {
                return;
            }

            var sid = SidTopLevelBrowser;
            var browserIid = IidShellBrowser;
            nint shown = 0;
            hr = ((delegate* unmanaged[Stdcall]<nint, Guid*, Guid*, nint*, int>)ComApi.Slot(provider.Pointer, SlotQueryService))(provider.Pointer, &sid, &browserIid, &shown);
            using var browser = new ComPtr(shown);
            if (hr < 0 || browser.IsNull)
            {
                return;
            }

            nint active = 0;
            hr = ((delegate* unmanaged[Stdcall]<nint, nint*, int>)ComApi.Slot(browser.Pointer, SlotQueryActiveShellView))(browser.Pointer, &active);
            using var view = new ComPtr(active);
            if (hr < 0 || view.IsNull)
            {
                return;
            }

            // A null item with "deselect others" leaves nothing selected.
            hr = ((delegate* unmanaged[Stdcall]<nint, nint, uint, int>)ComApi.Slot(view.Pointer, SlotSelectItem))(view.Pointer, 0, SvsiDeselectOthers);
            if (hr < 0)
            {
                Log.Debug(Scope, $"the desktop declined to clear its selection (0x{hr:X8})");
            }
        }
        catch (Exception ex)
        {
            Log.Debug(Scope, $"clearing the desktop selection threw: {ex.Message}");
        }
    }
}
