using Themia.Core.Accounts;
using Themia.Core.Model;
using Themia.Core.Tasks;
using Themia.Sdk;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1860] Whether a widget can show nothing useful until the user sets it up: no timer, no feed,
/// no symbol, no place, no account to read from. Such a widget's body is one "Set up" button that
/// opens its settings page, instead of a sentence telling the user where to go.
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
