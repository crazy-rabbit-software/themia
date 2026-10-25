using Themia.Core.Accounts;
using Themia.Sdk;

namespace Themia.App.Services.Accounts;

/// <summary>One account as widgets and ⚙ › Accounts see it: no secrets, plus whether it works.</summary>
/// <param name="Problem">
/// Why the account cannot be used right now, in words for ⚙ › Accounts — a revoked sign-in, a
/// token sealed on another PC. Null when it is fine. The account is kept either way.
/// </param>
public sealed record AccountInfo(
    string Id,
    ProviderId Provider,
    string Address,
    string DisplayName,
    string? Nickname,
    string? ServerUrl,
    string? Problem)
{
    /// <summary>The nickname, else the person's name, else the address.</summary>
    public string Label => Nickname ?? (DisplayName.Length > 0 ? DisplayName : Address);

    public FeatureSet Features => ProviderCatalog.FeaturesOf(Provider);

    public bool NeedsSignIn => Problem is not null;
}
