using Microsoft.UI.Dispatching;
using Themia.App.Services;
using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-6913] What a widget may ask of the host it runs in, handed to its module's
/// <see cref="IWidgetModule.CreateView"/>: the facade a view uses instead of reaching the host's
/// statics. One per card (the card's surface decides the ticker and whether it is a preview), every
/// member shared by all instances behind it, so however many widgets are on the desktop there is one
/// ticker, one launcher and one of each service.
/// <para>
/// Not here, and why: the notices are the SDK's own <c>Notices</c> (the host connects them to its
/// surfaces); the keyboard lease and text input are the card's (<see cref="Themia.App.Widgets.IWidgetSurface"/>,
/// through <see cref="Themia.App.Widgets.IWidgetSurfaceAware"/>); the theme and item fills are
/// <see cref="ThemeBrush"/> and <c>ItemFill</c>, installed once.
/// </para>
/// </summary>
public interface IWidgetHost
{
    /// <summary>The shared ticker every timing of the view comes from (the one <c>Attach</c> hands over too).</summary>
    Ticker Ticker { get; }

    /// <summary>The UI thread the view lives on: a service's callback is brought back here.</summary>
    DispatcherQueue Dispatcher { get; }

    /// <summary>The one place anything is opened.</summary>
    IShellLauncher Launcher { get; }

    /// <summary>[S-1251] The card is a preview (a debug render): a view whose buttons act on the machine is inert.</summary>
    bool IsPreview { get; }

    /// <summary>Open Settings › Accounts, for a widget whose empty state is "sign in". False where there is none to open (a debug render).</summary>
    bool ShowAccounts();

    /// <summary>[S-4863] The Recycle Bin: the one answer how full it is, and Windows' own empty.</summary>
    IRecycleBin RecycleBin { get; }

    /// <summary>
    /// [S-1243] [S-1251] What runs a power action: the machine's own calls on a desktop card, an inert
    /// backend that logs and runs nothing on a preview (a debug render). Asked when the view attaches.
    /// </summary>
    IPowerBackend Power { get; }

    /// <summary>[S-3907] The quick settings a toggle widget follows and switches: one shared source per toggle.</summary>
    IQuickToggles Toggles { get; }
}
