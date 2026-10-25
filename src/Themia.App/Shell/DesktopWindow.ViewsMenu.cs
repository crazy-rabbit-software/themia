using Themia.App.Services;
using Themia.Core.Model;
using Themia.Core.Settings;

namespace Themia.App.Shell;

/// <summary>
/// [S-3541] The edit bar's View menu (View ▾) on this display: this display's views and what each
/// offers, through the host's view commands. Opened and closed as every bar menu is (<see cref="OpenBarMenu"/>).
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-3541] The View menu's lines, as the state holds them now.</summary>
    private Func<IReadOnlyList<SettingsLine>> ViewsMenuLines()
    {
        var host = new SurfaceViewsHost(this);
        return () => ViewsMenu.Root(host);
    }

    /// <summary>[S-3546] A view chip's View settings: the View menu on this display, at that view's submenu.</summary>
    private void OpenViewSettings(string viewId) => OpenBarMenu(BarMenu.View, [ViewsMenu.Keys.View(viewId)]);

    /// <summary>[S-3541] What this display's View menu reads and writes: the views, shown, made and moved through the host, for this display.</summary>
    private sealed class SurfaceViewsHost(DesktopWindow surface) : IViewsMenuHost
    {
        private AppHost Host => surface._host;

        public AppState State => Host.State;

        public string DisplayId => surface._monitor.Id;

        public void Mutate(Action<AppState> change, bool relayout = true) => Host.Mutate(change, relayout);

        public void Show(string viewId) => Host.ShowView(DisplayId, viewId);

        public void Add() => _ = Host.AddView(DisplayId);

        public void Duplicate(string viewId) => Host.DuplicateView(viewId, DisplayId);

        public void Delete(string viewId) => Host.DeleteView(viewId);

        public void Reorder(string viewId, string targetId) => Host.ReorderView(viewId, targetId);

        public void Confirm(SettingsConfirm confirm) => surface._barMenus[BarMenu.View].Confirm(confirm);
    }
}
