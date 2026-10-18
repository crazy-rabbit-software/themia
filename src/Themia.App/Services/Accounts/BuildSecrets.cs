using Themia.Core.Diagnostics;
using Themia.Sdk.Services;

namespace Themia.App.Services.Accounts;

/// <summary>
/// [S-9917] The client secrets a provider insists on, compiled in from <c>THEMIA_CLIENT_SECRETS</c>
/// (<c>google=…;todoist=…</c>) by the build (<c>BuildSecrets.g.cs</c>, never committed) and read
/// through the one parsing rule, <see cref="ClientSecretRules"/>. The build already refused a value
/// that breaks the rule, so a parse problem here means a hand-made build; it is logged once and the
/// build holds no secret.
/// </summary>
internal static partial class BuildSecrets
{
    private const string Scope = "accounts.secrets";

    private static readonly Lazy<IReadOnlyDictionary<string, string>> Secrets = new(Load);

    /// <summary>The secret for a provider's <c>BuildSecret</c> key, or null when this build holds none.</summary>
    public static string? ClientSecret(string key)
        => Secrets.Value.TryGetValue(key, out var secret) ? secret : null;

    private static IReadOnlyDictionary<string, string> Load()
    {
        var secrets = ClientSecretRules.Parse(Raw, out var problem);
        if (secrets is null)
        {
            Log.Error(Scope, $"THEMIA_CLIENT_SECRETS was compiled in but does not parse ({problem}); no client secret is used");
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        Log.Info(Scope, $"client secrets in this build: {ClientSecretRules.Describe(secrets)}");
        return secrets;
    }
}
