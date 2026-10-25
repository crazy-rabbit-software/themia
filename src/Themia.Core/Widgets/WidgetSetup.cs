using Themia.Core.Accounts;
using Themia.Core.Model;
using Themia.Core.Tasks;
using Themia.Sdk;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1860] Whether a widget can show nothing useful until the user sets it up: no timer, no feed,
/// no symbol, no place, no account to read from. Such a widget's body is one "Set up" button that
/// goes where the missing piece is set ([S-1862], <see cref="Goes"/>), instead of a sentence telling
/// the user where to go.
/// <para>
/// A pure function of the config and the account features signed in, so the card decides it the
/// same way for every widget and it can be tested without a window. "Nothing right now" (no unread
/// mail, no events today, an empty folder) is not setup and stays the widget's own empty state; so
/// does something that is set up but wrong (a symbol that is not one, a deleted folder), whose own
/// sentence says what is wrong.
/// </para>
/// </summary>
public static class WidgetSetup
{
    /// <summary>
    /// True when <paramref name="config"/> leaves the widget with nothing to show until it is
    /// configured. <paramref name="signedIn"/> is every feature some signed-in account offers
    /// (the union of their providers' <see cref="AccountProviders.Features"/>).
    /// </summary>
    public static bool Needed(WidgetConfig config, FeatureSet signedIn)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config is ISetupRule rule && rule.NeedsSetup(signedIn);
    }

    /// <summary>
    /// [S-1862] Where the Set up button goes: ⚙ › Accounts when an account is all the widget
    /// lacks (it would be set up with every feature <paramref name="offerable"/> signed in, so adding
    /// an account is the step), else the widget's properties menu in edit mode, where its options are
    /// (an account widget that lacks something else too, a feed address say, goes there: its menu has
    /// the Accounts line as well, [S-3745]).
    /// </summary>
    public static SetupGoes Goes(WidgetConfig config, FeatureSet signedIn, FeatureSet offerable)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(signedIn);
        ArgumentNullException.ThrowIfNull(offerable);

        return config is ISetupRule rule && rule.NeedsSetup(signedIn) && !rule.NeedsSetup(offerable)
            ? SetupGoes.Accounts
            : SetupGoes.Menu;
    }

    /// <summary>Every feature the accounts' providers offer between them.</summary>
    public static FeatureSet Offered(IEnumerable<ProviderId> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var features = FeatureSet.None;
        foreach (var provider in providers)
        {
            features |= provider.Features();
        }

        return features;
    }
}

/// <summary>[S-1862] Where a widget's Set up button goes.</summary>
public enum SetupGoes
{
    /// <summary>Edit mode, the widget selected alone, its properties menu open at the line its module names.</summary>
    Menu,

    /// <summary>⚙ › Accounts: an account is all the widget lacks.</summary>
    Accounts,
}
