using Themia.Core.Accounts;
using Themia.Core.Model;
using Themia.Core.Tasks;

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
    public static bool Needed(WidgetConfig config, AccountFeatures signedIn)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config switch
        {
            CountdownConfig countdown => countdown.Target is null,
            QuoteConfig quote => quote.EffectiveQuotes.Count == 0,
            RssConfig rss => rss.ResolvedFeeds.Count == 0,
            StocksConfig stocks => string.IsNullOrWhiteSpace(stocks.Symbol),
            WeatherConfig weather => !weather.HasLocation,
            VideoStreamConfig stream => stream.ResolvedUrl.Problem == WebUrlProblem.Empty,
            ShortcutsConfig shortcuts => shortcuts.Items.Count == 0,
            GalleryConfig gallery => gallery.Source switch
            {
                GallerySource.Folder => string.IsNullOrWhiteSpace(gallery.Path),
                GallerySource.File => string.IsNullOrWhiteSpace(gallery.FilePath),
                _ => false,
            },

            EmailConfig => Lacks(signedIn, AccountFeatures.Mail),
            // [S-5325] The planner shows a calendar's days and, if it lists them, an account's
            // tasks: it waits for setup only while neither source is signed in.
            WeekPlannerConfig planner => Lacks(signedIn, AccountFeatures.Calendar) && (!planner.ShowTasks || Lacks(signedIn, AccountFeatures.Tasks)),
            CalendarConfig => Lacks(signedIn, AccountFeatures.Calendar),
            ContactsConfig => Lacks(signedIn, AccountFeatures.Contacts),

            // A list kept on this PC needs no account; it starts from the widget itself ([S-604]).
            TasksConfig tasks => tasks.Source == TaskSource.Account && Lacks(signedIn, AccountFeatures.Tasks),
            _ => false,
        };
    }

    /// <summary>Every feature the accounts' providers offer between them.</summary>
    public static AccountFeatures Offered(IEnumerable<AccountProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var features = AccountFeatures.None;
        foreach (var provider in providers)
        {
            features |= provider.Features();
        }

        return features;
    }

    private static bool Lacks(AccountFeatures signedIn, AccountFeatures wanted) => (signedIn & wanted) == 0;
}
