using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Sdk;

[assembly: ThemiaProvider("microsoft", typeof(MicrosoftProvider))]
[assembly: ThemiaProvider("google", typeof(GoogleProvider))]
[assembly: ThemiaProvider("imap", typeof(ImapProvider))]
[assembly: ThemiaProvider("caldav", typeof(CalDavProvider))]
[assembly: ThemiaProvider("carddav", typeof(CardDavProvider))]

namespace Themia.Core.Accounts;

/// <summary>
/// [S-500] The account providers the host serves itself, as provider modules declared inside Core
/// until their sign-in runs on descriptors (docs/WIDGET-KIT.md section 11.2, step 11). Each id is
/// the name the provider has always been written under in <c>accounts.json</c> ([S-501]), so the
/// file keeps its shape. Host code that still names a provider does it through these fields
/// (greppable for that step).
/// </summary>
public static class HostProviders
{
    /// <summary>A Microsoft account, personal or work: Graph, signed in through the browser (PKCE).</summary>
    public static readonly ProviderId Microsoft = new("microsoft");

    /// <summary>A Google account: Gmail, Calendar, Tasks and People, signed in through the browser.</summary>
    public static readonly ProviderId Google = new("google");

    /// <summary>A mail server with a user name and password.</summary>
    public static readonly ProviderId Imap = new("imap");

    /// <summary>A CalDAV calendar collection with a user name and password.</summary>
    public static readonly ProviderId CalDav = new("caldav");

    /// <summary>A CardDAV address book with a user name and password.</summary>
    public static readonly ProviderId CardDav = new("carddav");

    /// <summary>[S-502] Everything a full office account feeds.</summary>
    internal static readonly FeatureSet Office = FeatureSet.Of(HostFeatures.Mail, HostFeatures.Calendar, HostFeatures.Contacts, HostFeatures.Tasks);
}

public sealed class MicrosoftProvider : IProviderModule
{
    public ProviderId Id => HostProviders.Microsoft;

    public LocalText Label { get; } = LocalText.Fixed("Microsoft");

    public FeatureSet Features => HostProviders.Office;

    public AccountAuth Auth => AccountAuth.OAuthRefresh;

    public int AddRank => 0;

    public CadenceRow? Cadence => null;
}

public sealed class GoogleProvider : IProviderModule
{
    public ProviderId Id => HostProviders.Google;

    public LocalText Label { get; } = LocalText.Fixed("Google");

    public FeatureSet Features => HostProviders.Office;

    public AccountAuth Auth => AccountAuth.OAuthRefresh;

    public int AddRank => 1;

    public CadenceRow? Cadence => null;
}

public sealed class ImapProvider : IProviderModule
{
    public ProviderId Id => HostProviders.Imap;

    public LocalText Label => Str.Shell_Accounts_Provider_Imap.Local();

    public FeatureSet Features { get; } = HostFeatures.Mail;

    public AccountAuth Auth => AccountAuth.Password;

    public int AddRank => 2;

    public CadenceRow? Cadence => null;
}

public sealed class CalDavProvider : IProviderModule
{
    public ProviderId Id => HostProviders.CalDav;

    public LocalText Label => Str.Shell_Accounts_Provider_CalDav.Local();

    public FeatureSet Features { get; } = HostFeatures.Calendar;

    public AccountAuth Auth => AccountAuth.Password;

    public int AddRank => 3;

    public CadenceRow? Cadence => null;
}

public sealed class CardDavProvider : IProviderModule
{
    public ProviderId Id => HostProviders.CardDav;

    public LocalText Label => Str.Shell_Accounts_Provider_CardDav.Local();

    public FeatureSet Features { get; } = HostFeatures.Contacts;

    public AccountAuth Auth => AccountAuth.Password;

    public int AddRank => 4;

    public CadenceRow? Cadence => null;
}

/// <summary>Short forms for host code: a provider id's facts, read from its module ([S-6910]).</summary>
public static class AccountProviders
{
    /// <summary>What the settings page calls the provider.</summary>
    public static string Label(this ProviderId provider) => ProviderCatalog.Of(provider).Label.Text();

    /// <summary>[S-502] What the provider feeds. Follows from its module, never from a list of switches.</summary>
    public static FeatureSet Features(this ProviderId provider) => ProviderCatalog.Of(provider).Features;

    public static AccountAuth Auth(this ProviderId provider) => ProviderCatalog.Of(provider).Auth;
}
