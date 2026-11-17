using Themia.App.Services;
using Themia.Core.Model;
using Themia.Core.Settings;

namespace Themia.App.Shell;

/// <summary>
/// [S-3541] [S-2300] The edit bar's Views page on this display: the view it shows and every view's
/// order, names and deletion, through the host's view commands. Opened and closed as every page is (<see cref="OpenPage"/>).
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-3546] [S-2313] A view chip's View settings: the Views page open on this display at that view's card, opened.</summary>
    private void OpenViewSettings(string viewId)
        => OpenPage(SettingsPageId.Views, [ViewsMenu.Keys.Heading, ViewsMenu.Keys.View(viewId)]);

    /// <summary>[S-3541] What this display's Views page reads and writes: the views, shown, made and moved through the host, for this display.</summary>
    private sealed class SurfaceViewsHost(DesktopWindow surface) : IViewsMenuHost
    {
        private AppHost Host => surface._host;

        public AppState State => Host.State;

        public string DisplayId => surface._monitor.Id;

        public IReadOnlyList<MenuDisplay> Displays => [.. Host.Displays.Select(d => new MenuDisplay(d.Id, d.ShortName, d.Describe(), d.Info))];

        public void Show(string viewId) => Host.ShowView(DisplayId, viewId);

        public void Mutate(Action<AppState> change, bool relayout = true) => Host.Mutate(change, relayout);

        public void Add() => _ = Host.AddView(DisplayId);

        public void Duplicate(string viewId) => Host.DuplicateView(viewId, DisplayId);

        public void Delete(string viewId) => Host.DeleteView(viewId);

        public void Reorder(string viewId, string targetId) => Host.ReorderView(viewId, targetId);

        public void Confirm(SettingsConfirm confirm) => surface.ConfirmOnFlyout(confirm);

        public IReadOnlyList<Themia.Sdk.DesktopItem> DesktopItems => Services.Files.HostDesktopItems.Instance.Items;

        public void SetDesktopIcons(string viewId, ViewDesktopIcons value) => Host.SetViewDesktopIcons(viewId, value);

        public void ShowDesktopItem(string viewId, string key, bool shown) => Host.ShowDesktopItemOn(viewId, key, shown);
    }
}
