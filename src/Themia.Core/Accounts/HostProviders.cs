using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Sdk;
using Themia.Sdk.Services;

[assembly: ThemiaProvider("microsoft", typeof(MicrosoftProvider))]
[assembly: ThemiaProvider("google", typeof(GoogleProvider))]
[assembly: ThemiaProvider("imap", typeof(ImapProvider))]
[assembly: ThemiaProvider("caldav", typeof(CalDavProvider))]
[assembly: ThemiaProvider("carddav", typeof(CardDavProvider))]

namespace Themia.Core.Accounts;


public sealed class MicrosoftProvider : IProviderModule
{
    public ProviderId Id => HostProviders.Microsoft;

    public LocalText Label { get; } = LocalText.Fixed("Microsoft");

    public FeatureSet Features => HostProviders.Office;

    public AccountAuth Auth => AccountAuth.OAuthRefresh;

    public int AddRank => 0;

    public CadenceRow? Cadence => null;

    /// <summary>[S-1780] [S-9910] The browser flow as a public client: no secret, a <c>localhost</c> redirect on any port.</summary>
    public ProviderSignIn SignIn { get; } = new([OAuthClients.Microsoft.SignIn]);
}

public sealed class GoogleProvider : IProviderModule
{
    public ProviderId Id => HostProviders.Google;

    public LocalText Label { get; } = LocalText.Fixed("Google");

    public FeatureSet Features => HostProviders.Office;

    public AccountAuth Auth => AccountAuth.OAuthRefresh;

    public int AddRank => 1;

    public CadenceRow? Cadence => null;

    /// <summary>[S-527] [S-9910] The browser flow with the Desktop client's build-time secret ([S-536]) and a <c>127.0.0.1</c> redirect on any port.</summary>
    public ProviderSignIn SignIn { get; } = new([OAuthClients.Google.SignIn]);

    /// <summary>[S-9917] Its key in <c>THEMIA_CLIENT_SECRETS</c>.</summary>
    public string? BuildSecret => "google";
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
    /// <summary>What ⚙ › Accounts calls the provider.</summary>
    public static string Label(this ProviderId provider) => ProviderCatalog.Of(provider).Label.Text();

    /// <summary>[S-502] What the provider feeds. Follows from its module, never from a list of switches.</summary>
    public static FeatureSet Features(this ProviderId provider) => ProviderCatalog.Of(provider).Features;

    public static AccountAuth Auth(this ProviderId provider) => ProviderCatalog.Of(provider).Auth;
}
