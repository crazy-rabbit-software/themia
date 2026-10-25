using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Settings;

namespace Themia.App.Shell;

/// <summary>
/// [S-3543] The edit bar's Display menu (Display ▾) on this display: its scale and view bar, and what
/// every display shares, through the host. Opened and closed as every bar menu is (<see cref="OpenBarMenu"/>).
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-3543] The Display menu's lines, as the state and the connected displays are now.</summary>
    private Func<IReadOnlyList<SettingsLine>> DisplayMenuLines()
    {
        var host = new SurfaceDisplayHost(this);
        return () => DisplayMenu.Root(host);
    }

    /// <summary>[S-3545] A display pass ran: an open Display menu lists the displays as they are now (one plugged in, one switched off).</summary>
    internal void DisplaysPassed()
    {
        if (_openBarMenu == BarMenu.Display)
        {
            SyncBarMenu();
        }
    }

    /// <summary>[S-3543] What this display's Display menu reads and writes: the state, the displays the last pass found, and the host's switch and scale.</summary>
    private sealed class SurfaceDisplayHost(DesktopWindow surface) : IDisplayMenuHost
    {
        private AppHost Host => surface._host;

        public AppState State => Host.State;

        public string DisplayId => surface._monitor.Id;

        public IReadOnlyList<MenuDisplay> Displays => [.. Host.Displays.Select(d => new MenuDisplay(d.Id, d.ShortName, d.Describe(), d.Info))];

        public void Mutate(Action<AppState> change, bool relayout = true) => Host.Mutate(change, relayout);

        public void SetScale(double scale) => Host.SetDisplayScale(DisplayId, scale);

        /// <summary>
        /// [S-3545] Another display's switch is thrown at once (the display pass builds or closes its
        /// surface). This display's own "off" closes the surface the click came from, so it is run once
        /// the click has finished, with its menu already put away.
        /// </summary>
        public bool SetEnabled(string displayId, bool on)
        {
            if (on || displayId != DisplayId)
            {
                return Host.SetDisplayEnabled(displayId, on);
            }

            surface.CloseBarMenu(focusButton: false);
            if (surface._root.DispatcherQueue is not { } queue || !queue.TryEnqueue(() => Host.SetDisplayEnabled(displayId, false)))
            {
                Log.Error(Scope, "could not queue switching Themia off on this display");
                return false;
            }

            return true;
        }
    }
}
