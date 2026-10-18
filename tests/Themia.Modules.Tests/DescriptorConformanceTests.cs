using Themia.Sdk;
using Themia.Sdk.Services;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-9930] The "Descriptors" conformance of docs/WIDGET-KIT.md section 9, over every provider this
/// build registers (so a new provider project is checked by being there): its sign-in members and
/// its service descriptor are data the host can run, and the secret it asks the build for is one the
/// one parsing rule accepts.
/// </summary>
public sealed class DescriptorConformanceTests
{
    public static TheoryData<string> Providers()
    {
        var data = new TheoryData<string>();
        foreach (var provider in ProviderCatalog.All)
        {
            data.Add(provider.Id.Value);
        }

        return data;
    }

    private static IProviderModule Of(string id) => ProviderCatalog.All.Single(p => p.Id.Value == id);

    private static bool Https(Uri? uri) => uri is { IsAbsoluteUri: true } && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo);

    [Theory]
    [MemberData(nameof(Providers))]
    public void S9930_every_sign_in_member_names_https_addresses_and_a_client(string id)
    {
        foreach (var member in Of(id).SignIn.Members)
        {
            switch (member)
            {
                case OAuthPkceLoopback browser:
                    Assert.True(Https(browser.AuthorizeUrl) && Https(browser.TokenUrl), $"{id}: a browser member's addresses are https");
                    Assert.False(string.IsNullOrWhiteSpace(browser.ClientId));
                    Assert.All(browser.Loopback.Ports ?? [], p => Assert.InRange(p, 1024, 65535));
                    Assert.True(browser.Identity is null || Https(browser.Identity.Url));
                    break;
                case OAuthDeviceCode device:
                    Assert.True(Https(device.DeviceUrl) && Https(device.TokenUrl), $"{id}: a device member's addresses are https");
                    Assert.False(string.IsNullOrWhiteSpace(device.ClientId));
                    Assert.True(device.GrantPage is null || Https(device.GrantPage));
                    break;
                case PastedToken paste:
                    Assert.True(Https(paste.TokenPage), $"{id}: a token page is https");
                    Assert.True(paste.Shape.MinLength > 0 && paste.Shape.MaxLength >= paste.Shape.MinLength && paste.Shape.Alphabet.Length > 0);
                    break;
            }
        }
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void S9930_a_build_time_secret_has_a_key_the_one_rule_accepts_and_only_then(string id)
    {
        var provider = Of(id);
        var needs = provider.SignIn.Members.OfType<OAuthPkceLoopback>().Any(m => m.ClientSecret == ClientSecretRule.BuildTime);

        Assert.Equal(needs, provider.BuildSecret is not null);
        if (needs)
        {
            Assert.True(ClientSecretRules.IsKey(provider.BuildSecret), $"{id}: '{provider.BuildSecret}' is not a key THEMIA_CLIENT_SECRETS can hold");
        }
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void S9930_a_kit_service_is_data_the_host_can_run(string id)
    {
        if (Of(id).Service is not { } service)
        {
            return;
        }

        Assert.Equal(Of(id).Id, service.Provider);
        Assert.Equal(service.Provider.Value, service.Feature.Value); // one feature per kit service, its provider's id
        Assert.Contains(service.Feature, Of(id).Features.Members);
        Assert.True(Https(service.ApiRoot), $"{id}: the API root is https");
        Assert.NotEmpty(service.SignIn.Members);
        Assert.Equal(Of(id).SignIn.Members, service.SignIn.Members);
        Assert.NotEmpty(service.WebHosts);
        Assert.All(service.WebHosts, h => Assert.True(Uri.CheckHostName(h) == UriHostNameType.Dns && !h.Contains('/', StringComparison.Ordinal), $"{id}: '{h}' is a host name"));
        Assert.All(service.Pinned, p => Assert.False(p.Name.Equals("Authorization", StringComparison.OrdinalIgnoreCase), $"{id}: a pinned header is never a credential"));
        Assert.Contains(429, service.Limits.ThrottleStatuses);
        Assert.InRange(service.MaxPages, 1, 50);
        Assert.InRange(service.ItemBound, 1, 10_000);
        Assert.True(service.IdleFloor >= TimeSpan.FromSeconds(30));
        Assert.Contains(service.Cadence.DefaultMinutes, service.Cadence.Choices);
        Assert.Same(service.Cadence, Of(id).Cadence);
        Assert.True(!service.Anonymous.PublicReads || service.Anonymous.Floor >= service.IdleFloor);
        Assert.True(service.Transport != TransportKind.GraphQL || service.Errors.Body == ErrorBodyKind.GraphQLErrors, $"{id}: a GraphQL service reads its errors array");
    }

    [Fact]
    public void S9930_no_two_providers_share_a_cadence_row_or_a_build_secret()
    {
        var rows = ProviderCatalog.All.Select(p => p.Cadence?.Id.Value).OfType<string>().ToList();
        Assert.Equal(rows.Count, rows.Distinct(StringComparer.Ordinal).Count());

        var secrets = ProviderCatalog.All.Select(p => p.BuildSecret).OfType<string>().ToList();
        Assert.Equal(secrets.Count, secrets.Distinct(StringComparer.Ordinal).Count());
    }
}
