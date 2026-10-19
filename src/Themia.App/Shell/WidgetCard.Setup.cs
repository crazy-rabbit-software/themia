using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Shell;

/// <summary>
/// [S-1862] A widget that cannot show anything until it is set up gets one "Set up" button in place
/// of its body, decided here for every widget by <see cref="WidgetSetup.Needed"/> rather than by
/// each view in its own words.
/// <para>
/// [S-1881] The view under it is parked: built but not attached, so it ticks, polls and subscribes to
/// nothing, and logs nothing. The moment the config (or a newly signed-in account) makes the widget
/// useful the button goes and a fresh view is attached in its place; a widget whose config is emptied
/// again is detached and parked. The one widget set up by using it, an empty Shortcuts widget, keeps
/// running under the button so a file can be dropped onto it. The check runs whenever the config
/// arrives, when the view saves a change itself (a shortcut dropped onto it), and when the accounts
/// change.
/// </para>
/// </summary>
internal sealed partial class WidgetCard
{
    private SetupPrompt? _setup;

    /// <summary>The account service this card listens to, kept so Detach unhooks the one it hooked.</summary>
    private Services.Accounts.AccountService? _setupAccounts;

    /// <summary>Whether the card is showing the Set up button instead of its view.</summary>
    public bool ShowsSetup => _setup is not null;

    /// <summary>[S-1881] The view is built but held back, unattached, because the widget needs setting up.</summary>
    private bool _parked;

    /// <summary>Every account feature signed in now. The card's own service once hooked, the host's before.</summary>
    private FeatureSet SignedIn()
        => WidgetSetup.Offered((_setupAccounts ?? AppServices.Accounts)?.Accounts.Select(a => a.Provider) ?? []);

    private void AttachSetup()
    {
        _setupAccounts = AppServices.Accounts;
        if (_setupAccounts is not null)
        {
            _setupAccounts.AccountsChanged += ApplySetup;
        }

        ApplySetup();
    }

    private void DetachSetup()
    {
        if (_setupAccounts is not null)
        {
            _setupAccounts.AccountsChanged -= ApplySetup;
            _setupAccounts = null;
        }
    }

    /// <summary>Show or take away the button to match the config and the accounts signed in now.</summary>
    private void ApplySetup()
    {
        var signedIn = SignedIn();
        var needed = !IsFaulted && WidgetSetup.Needed(Model.Config, signedIn);

        if (needed && _setup is null)
        {
            _setup = new SetupPrompt(ModuleCatalog.Of(Model.Kind).Glyph, () => _host.RequestSetup(this));
            Grid.SetRow(_setup, 1);
            Grid.SetColumn(_setup, 1);
            _content.Children.Add(_setup);
        }
        else if (!needed && _setup is not null)
        {
            _content.Children.Remove(_setup);
            _setup = null;
        }

        // [S-1881] Run exactly when the widget is useful. Unparking swaps a fresh view in, which runs
        // this check again for the new view, so this pass ends there.
        if (!IsFaulted && Unpark(WidgetLiveness.Runs(Model.Config, signedIn)))
        {
            return;
        }

        // Every time, not only on a change: a retried view arrives fresh, fully visible.
        _view.Opacity = _setup is null ? 1 : 0;
        _view.IsHitTestVisible = _setup is null;
    }

    /// <summary>
    /// [S-1881] Park a running view that should not run (detach it; it stays, invisible, under the
    /// button), or start a fresh one in place of a parked view that should. True when a fresh view was
    /// swapped in.
    /// </summary>
    private bool Unpark(bool runs)
    {
        if (!runs && _running)
        {
            DetachQuietly();
            _parked = true;
            Log.Debug(Scope, $"widget '{Model.Id}' ({Model.Kind}) needs setting up; its view is stopped");
            return false;
        }

        if (!runs || !_parked)
        {
            return false;
        }

        var parked = _view;
        _view = CreateViewNow(Model.Kind);
        AttachView();
        Swap(parked, _view);
        return true;
    }

#if DEBUG
    /// <summary>
    /// Debug renders only ([S-2711]): run the view though the widget would need setting up, so a
    /// preview state can draw it over fixture data. A render has no accounts, so without this every
    /// account widget is its Set up button.
    /// </summary>
    internal void RunForPreview()
    {
        if (_setup is not null)
        {
            _content.Children.Remove(_setup);
            _setup = null;
        }

        _view.Opacity = 1;
        _view.IsHitTestVisible = true;

        if (_running || _view is not IWidgetView widgetView)
        {
            return;
        }

        _parked = false;
        if (_view is IWidgetSurfaceAware aware)
        {
            aware.Bind(this);
        }

        _running = true;
        Gallery.LiveCensus.ViewAttached(_view);
        widgetView.Attach(_host.Ticker, Model.Config);

        // The slot was measured while the view was parked, which it did not hear ([S-1881]).
        if (_view is IWidgetSizeAware sizeAware && _view.ActualWidth > 0 && _view.ActualHeight > 0)
        {
            sizeAware.ApplySize(new WidgetArea(_view.ActualWidth, _view.ActualHeight));
        }
    }
#endif
}