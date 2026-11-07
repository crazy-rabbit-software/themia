using Themia.Core.Accounts;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Services.Accounts;

/// <summary>
/// [S-2005] The web page of an account's own service for one feature (Outlook's calendar, Gmail):
/// what a click on an account widget opens. The pages are fixed https addresses per provider; a
/// provider with none (IMAP, CalDAV, CardDAV) opens nothing.
/// </summary>
public static class AccountPages
{
    /// <summary>
    /// [S-2005] Of the accounts the widget reads (<paramref name="chosen"/>, or every account with the
    /// feature when it names none), the first whose provider has a page for <paramref name="feature"/>.
    /// Null with no such account.
    /// </summary>
    public static CardOpen? Home(FeatureId feature, IReadOnlyList<string> chosen, IReadOnlyList<AccountInfo> accounts)
    {
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentNullException.ThrowIfNull(accounts);

        var reading = chosen.Count == 0
            ? accounts.Where(a => a.Features.Has(feature))
            : chosen.Select(id => accounts.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.Ordinal)))
                .OfType<AccountInfo>()
                .Where(a => a.Features.Has(feature));

        foreach (var account in reading)
        {
            if (Page(account.Provider, feature) is { } page)
            {
                return CardOpen.Web(page);
            }
        }

        return null;
    }

    /// <summary>The provider's own page for a feature, or null where it has none.</summary>
    public static string? Page(ProviderId provider, FeatureId feature)
    {
        if (provider == HostProviders.Microsoft)
        {
            return feature == HostFeatures.Mail ? "https://outlook.office.com/mail/"
                : feature == HostFeatures.Calendar ? "https://outlook.office.com/calendar/"
                : feature == HostFeatures.Contacts ? "https://outlook.office.com/people/"
                : feature == HostFeatures.Tasks ? "https://to-do.office.com/tasks/"
                : null;
        }

        if (provider == HostProviders.Google)
        {
            return feature == HostFeatures.Mail ? "https://mail.google.com/"
                : feature == HostFeatures.Calendar ? "https://calendar.google.com/"
                : feature == HostFeatures.Contacts ? "https://contacts.google.com/"
                : feature == HostFeatures.Tasks ? "https://tasks.google.com/"
                : null;
        }

        return null;
    }
}
