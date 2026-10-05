using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Widgets;

namespace Themia.App.Shell;

/// <summary>
/// [S-1862] A widget that cannot show anything until it is set up gets one "Set up" button in place
/// of its body, decided here for every widget by <see cref="WidgetSetup.Needed"/> rather than by
/// each view in its own words.
/// <para>
/// The view keeps running under it — it still hears its config and its slot — but is invisible and
/// takes no input, so the moment the config (or a newly signed-in account) makes it useful the
/// button goes and the view is simply there. The check runs whenever the config arrives, when the
/// view saves a change itself (a shortcut dropped onto it), and when the accounts change.
/// </para>
/// </summary>
internal sealed partial class WidgetCard
{
    private SetupPrompt? _setup;

    /// <summary>The account service this card listens to, kept so Detach unhooks the one it hooked.</summary>
    private Services.Accounts.AccountService? _setupAccounts;

    /// <summary>Whether the card is showing the Set up button instead of its view.</summary>
    public bool ShowsSetup => _setup is not null;

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
        var signedIn = WidgetSetup.Offered(_setupAccounts?.Accounts.Select(a => a.Provider) ?? []);
        var needed = !IsFaulted && WidgetSetup.Needed(Model.Config, signedIn);

        if (needed && _setup is null)
        {
            _setup = new SetupPrompt(WidgetCatalog.Definition(Model.Kind).Glyph, () => _host.RequestConfigure(this));
            Grid.SetRow(_setup, 1);
            Grid.SetColumn(_setup, 1);
            _content.Children.Add(_setup);
        }
        else if (!needed && _setup is not null)
        {
            _content.Children.Remove(_setup);
            _setup = null;
        }

        // Every time, not only on a change: a retried view arrives fresh, fully visible.
        _view.Opacity = _setup is null ? 1 : 0;
        _view.IsHitTestVisible = _setup is null;
    }
}
