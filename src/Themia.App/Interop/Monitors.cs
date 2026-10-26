using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Interop;

/// <summary>One display.</summary>
/// <param name="Id">
/// [S-842] Stable id every per-display setting is keyed by: the monitor's device interface path, or its
/// GDI name when that could not be read (<paramref name="IdResolved"/> false, [S-843]).
/// </param>
/// <param name="IdResolved">Whether <paramref name="Id"/> is a real device path rather than the ordinal.</param>
/// <param name="GdiName">The GDI device name (<c>\\.\DISPLAY1</c>). An ordinal: for logs only, never a key.</param>
/// <param name="Bounds">The whole monitor in <b>physical</b> pixels. The surface covers exactly this ([S-840]).</param>
/// <param name="WorkArea">The monitor less the taskbar, in <b>physical</b> pixels: where new geometry may go ([S-841]).</param>
/// <param name="IsPrimary">Whether this is the primary display.</param>
/// <param name="FriendlyName">The monitor's product name, or empty when the display cannot say.</param>
internal readonly record struct MonitorTarget(
    string Id,
    bool IdResolved,
    string GdiName,
    Win32.Rect Bounds,
    Win32.Rect WorkArea,
    bool IsPrimary,
    string FriendlyName = "")
{
    /// <summary>The display as the pure display pass sees it.</summary>
    public DisplayInfo Info => new(Id, IdResolved, ToPixels(Bounds), ToPixels(WorkArea), IsPrimary);

    /// <summary>Whether a physical screen point is on this display, taskbar included.</summary>
    public bool Contains(int x, int y)
        => x >= Bounds.Left && x < Bounds.Right && y >= Bounds.Top && y < Bounds.Bottom;

    /// <summary>
    /// What to call this display in the UI: its product name where known, plus its resolution so two
    /// identical monitors are still told apart, and which one is primary.
    /// </summary>
    public string ShortName
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(FriendlyName) ? Nickname() : FriendlyName;
            // "Main display" already says it is the primary one; only a product name needs the mark.
            return IsPrimary && !string.IsNullOrWhiteSpace(FriendlyName) ? Str.Shell_Display_Primary.Format(("name", name)) : name;
        }
    }

    /// <summary>[S-3507] What the edit bar's Display crumb says: the product name where known, else where the screen is.</summary>
    public string Name => string.IsNullOrWhiteSpace(FriendlyName) ? Nickname() : FriendlyName;

    /// <summary>The long form: a display's name with its size, for the Display menu.</summary>
    public string Describe()
    {
        var name = string.IsNullOrWhiteSpace(FriendlyName) ? Nickname() : FriendlyName;
        var size = Str.Shell_Display_Size.Format(("width", LocaleFormat.Integer(Bounds.Width)), ("height", LocaleFormat.Integer(Bounds.Height)));
        return IsPrimary && !string.IsNullOrWhiteSpace(FriendlyName)
            ? Str.Shell_Display_DescribePrimary.Format(("name", name), ("size", size))
            : Str.Shell_Display_Describe.Format(("name", name), ("size", size));
    }

    private static PixelRect ToPixels(Win32.Rect r) => new(r.Left, r.Top, r.Width, r.Height);

    /// <summary>
    /// A fallback when the monitor reports no product name. The GDI device name is meaningless to a
    /// person, so say where the screen is instead — that, at least, they can check.
    /// </summary>
    private string Nickname()
    {
        if (IsPrimary)
        {
            return Str.Shell_Display_Main.Text();
        }

        return Bounds.Left < 0 ? Str.Shell_Display_Left.Text() : Bounds.Left > 0 ? Str.Shell_Display_Right.Text() : Str.App_MonitorLabelUnknown.Text();
    }
}

/// <summary>
/// Enumerates displays through <c>EnumDisplayMonitors</c>.
/// <para>
/// Not <see cref="Microsoft.UI.Windowing.DisplayArea"/>: <c>DisplayArea.FindAll()</c> throws an
/// <see cref="InvalidCastException"/> out of the CsWinRT projection here, so the one API that would
/// hand back work areas already in AppWindow coordinates is unusable. The window therefore converts
/// these physical rects itself (see <c>DesktopWindow.ApplyPlacement</c>).
/// </para>
/// </summary>
internal static unsafe class Monitors
{
    private const string Scope = "monitors";
    private const uint MonitorPrimary = 0x00000001;

    private static readonly Lock Gate = new();
    private static List<MonitorTarget>? _collecting;
    private static Dictionary<string, DisplayDescriptor> _described = [];

    /// <summary>
    /// All displays, primary first. Returns an empty list rather than throwing: a transient failure
    /// during a display change must not take the process down.
    /// </summary>
    public static List<MonitorTarget> All()
    {
        lock (Gate)
        {
            _collecting = [];
            // Read before the enumeration, never inside its callback: the callback runs inside the
            // display driver's walk and must do nothing that can block or fail slowly.
            _described = DisplayNames.Describe();
            try
            {
                var callback = (nint)(delegate* unmanaged<nint, nint, Win32.Rect*, nint, int>)&EnumCallback;
                if (!Win32.EnumDisplayMonitors(0, 0, callback, 0))
                {
                    Log.Warn(Scope, $"EnumDisplayMonitors failed (win32 {Marshal.GetLastWin32Error()})");
                }

                var found = _collecting;
                found.Sort(static (a, b) => b.IsPrimary.CompareTo(a.IsPrimary));

                foreach (var monitor in found.Where(m => !m.IdResolved))
                {
                    // [S-843] Legitimate for a virtual or remote display, transient for a panel that is
                    // still waking. Either way it must be visible in the log when screens swap views.
                    Log.Warn(Scope, $"no device path for {monitor.GdiName}; using its ordinal until it reports one");
                }

                // [S-843] Monitors that report one device path each still get a display of their own.
                var ids = DisplayIdentity.Separate([.. found.Select(m => (m.Id, m.IdResolved, m.GdiName))]);
                for (var i = 0; i < found.Count; i++)
                {
                    if (ids[i].Id != found[i].Id)
                    {
                        Log.Warn(Scope, $"{found[i].GdiName} reports the device path of another display; using its ordinal");
                        found[i] = found[i] with { Id = ids[i].Id, IdResolved = ids[i].Resolved };
                    }
                }

                return found;
            }
            finally
            {
                _collecting = null;
            }
        }
    }

    [UnmanagedCallersOnly]
    private static int EnumCallback(nint monitor, nint hdc, Win32.Rect* clip, nint data)
    {
        // A Win32 callback must never let a managed exception unwind into native code: that is
        // undefined behaviour, and it would happen inside the display driver's enumeration.
        try
        {
            var info = new Win32.MonitorInfoEx { Size = sizeof(Win32.MonitorInfoEx) };

            if (!Win32.GetMonitorInfo(monitor, ref info))
            {
                return 1; // skip this one, keep enumerating
            }

            var name = info.DeviceName;
            var gdi = string.IsNullOrEmpty(name) ? $"display-{monitor}" : name;
            _described.TryGetValue(gdi, out var described);
            var (id, resolved) = DisplayIdentity.Resolve(described.DevicePath, gdi);

            _collecting?.Add(new MonitorTarget(
                id,
                resolved,
                gdi,
                info.Monitor,
                info.Work,
                (info.Flags & MonitorPrimary) != 0,
                described.FriendlyName ?? string.Empty));
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "monitor enumeration callback threw", ex);
        }

        return 1;
    }
}
