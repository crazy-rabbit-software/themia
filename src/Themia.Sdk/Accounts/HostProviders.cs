using Themia.Sdk;

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
    public static readonly FeatureSet Office = FeatureSet.Of(HostFeatures.Mail, HostFeatures.Calendar, HostFeatures.Contacts, HostFeatures.Tasks);
}
