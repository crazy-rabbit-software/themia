#if DEBUG
using Microsoft.UI.Dispatching;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.Core.Model;
using Themia.Core.Storage;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-69] Debug only: the real host and its real surfaces on stand-in displays, off-screen, for the checks that need
/// the desktop itself (desk select, displays, first drag, the drag bench, startup, switch). Built over a state the
/// check makes, on a detached store (<c>%TEMP%\themia-&lt;name&gt;\state.json</c>: never the user's), with a surface per
/// stand-in display the check adds; <see cref="ReadyAsync"/> is its one wait. Disposed, the surfaces close and the host goes.
/// </summary>
internal sealed class StandInDesktop : IDisposable
{
    private readonly List<DesktopWindow> _surfaces = [];

    private Func<bool>? _checkButton;

    private StandInDesktop(AppHost host) => Host = host;

    public AppHost Host { get; }

    /// <summary>The surfaces added, in order.</summary>
    public IReadOnlyList<DesktopWindow> Surfaces => _surfaces;

    /// <summary>The host over <paramref name="state"/>, its store detached under the check's <paramref name="name"/>.</summary>
    public static StandInDesktop Create(string name, AppState state)
        => new(new AppHost(
            DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("a stand-in desktop is built on the UI thread"),
            AppStateStore.Detached(Path.Combine(Path.GetTempPath(), $"themia-{name}", "state.json")),
            state));

    /// <summary>A display's bounds off-screen: <paramref name="width"/> × <paramref name="height"/> pixels, <paramref name="left"/> pixels right of the far corner.</summary>
    public static Win32.Rect OffScreen(int width, int height, int left = 0)
        => new() { Left = -32000 + left, Top = -32000, Right = -32000 + left + width, Bottom = -32000 + height };

    /// <summary>A stand-in display (<paramref name="id"/>, its <paramref name="bounds"/>, a taskbar of <paramref name="taskbar"/> pixels at the bottom).</summary>
    public static MonitorTarget Display(string id, Win32.Rect bounds, int taskbar = 48)
        => new(id, IdResolved: true, id, bounds, bounds with { Bottom = bounds.Bottom - taskbar }, IsPrimary: false);

    /// <summary>A surface for <paramref name="monitor"/>, adopted by the host; shown by <see cref="Show"/>.</summary>
    public DesktopWindow Add(MonitorTarget monitor)
    {
        var surface = new DesktopWindow(Host, monitor) { CheckButton = _checkButton };
        Host.AdoptSurfaceForCheck(surface);
        _surfaces.Add(surface);
        return surface;
    }

    /// <summary>
    /// The check's own mouse button on every surface, those added later too (null: the physical mouse's again), as
    /// <see cref="DesktopWindow.CheckButton"/>: while set, no surface takes the foreground from the user's window ([S-14320]),
    /// each counts the times it would have. For the checks that press through the host or the cards' own steps.
    /// </summary>
    public void UseCheckButton(Func<bool>? held)
    {
        _checkButton = held;
        foreach (var surface in _surfaces)
        {
            surface.CheckButton = held;
        }
    }

    /// <summary>Show <paramref name="surface"/> (or every surface added); <see cref="ReadyAsync"/> waits until they stand.</summary>
    public void Show(DesktopWindow? surface = null)
    {
        foreach (var shown in surface is null ? _surfaces : [surface])
        {
            shown.ShowSurface();
        }
    }

    /// <summary>
    /// [S-15902] The stand-in desktop's ready signal, never a fixed sleep: every surface (or <paramref name="surface"/>)
    /// at rest (its root loaded, a usable size, its first layout pass run, no intro playing:
    /// <see cref="DesktopWindow.NotAtRestForCheck"/>) and settled ([S-15900], <see cref="RenderHarness.SettleAsync"/>), each
    /// under the ceiling; one past it is said loudly as <paramref name="what"/>'s. Returns whether every one settled.
    /// <paramref name="looks"/> false, for a check that draws no picture (the first drag, the benches): at rest and nothing
    /// loading is ready, its pictures are not compared (a live reading, the PC's own sound in a visualizer, moves forever).
    /// </summary>
    public async Task<bool> ReadyAsync(string what, DesktopWindow? surface = null, bool looks = true)
    {
        var all = true;
        foreach (var shown in surface is null ? _surfaces : [surface])
        {
            all &= await RenderHarness.SettleAsync(shown.DebugRoot, $"{what} ({shown.Monitor.Id})", busy: () => shown.NotAtRestForCheck, looks: looks).ConfigureAwait(true);
        }

        return all;
    }

    public void Dispose()
    {
        for (var i = _surfaces.Count - 1; i >= 0; i--)
        {
            _surfaces[i].CloseSurface();
        }

        Host.Dispose();
    }
}
#endif
