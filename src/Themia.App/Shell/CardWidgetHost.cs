using Microsoft.UI.Dispatching;
using Themia.App.Services;
using Themia.App.Services.Toggles;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>
/// [S-6913] The host as a card's view sees it: the card's surface decides the ticker and whether it
/// is a preview; the launcher and the Accounts page are the app's one of each.
/// </summary>
internal sealed class CardWidgetHost(IWidgetCardHost card) : IWidgetHost
{
    public Ticker Ticker => card.Ticker;

    public DispatcherQueue Dispatcher { get; } = DispatcherQueue.GetForCurrentThread()
        ?? throw new InvalidOperationException("a widget card is built on the UI thread");

    public IShellLauncher Launcher => HostLauncher.Instance;

    public bool IsPreview => card.IsPreview;

    public IRecycleBin RecycleBin => HostRecycleBin.Instance;

    /// <summary>[S-1251] Inert unless the card is a real widget on the desktop.</summary>
    public IPowerBackend Power => card.IsPreview ? InertPowerActions.Instance : Win32PowerActions.Instance;

    public IQuickToggles Toggles => HostToggles.Instance;

    public bool ShowAccounts()
    {
        if (AppServices.ShowAccounts is not { } show)
        {
            return false;
        }

        show();
        return true;
    }
}
