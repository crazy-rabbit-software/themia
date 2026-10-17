using Themia.Core.Accounts;

namespace Themia.Sdk;

/// <summary>
/// [S-6910] The providers compiled into this build, installed once by the host from the generated
/// registry beside <see cref="ModuleCatalog"/>, before anything reads an account. Every question
/// about a provider (its name, its features, how it signs in, where the Add account menu lists it)
/// is answered here; there is no second table.
/// </summary>
public static class ProviderCatalog
{
    private sealed record Installed(IReadOnlyList<IProviderModule> All, Dictionary<ProviderId, IProviderModule> ById);

    private static Installed? _installed;

    public static bool IsInstalled => Volatile.Read(ref _installed) is not null;

    /// <summary>Every provider, in the Add account menu's order (<see cref="IProviderModule.AddRank"/>, then id).</summary>
    public static IReadOnlyList<IProviderModule> All => Current.All;

    /// <summary>Install the registry's providers. Once per process; a duplicate or empty id is refused loudly.</summary>
    public static void Install(IEnumerable<IProviderModule> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var list = providers.ToList();
        var problems = new List<string>();
        var ids = new HashSet<ProviderId>();
        foreach (var provider in list)
        {
            if (provider?.Id.Value is null)
            {
                problems.Add("a provider has no id");
            }
            else if (!ids.Add(provider.Id))
            {
                problems.Add($"provider id '{provider.Id}' is declared twice");
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException("the provider registry is inconsistent: " + string.Join("; ", problems));
        }

        var ordered = list.OrderBy(p => p.AddRank).ThenBy(p => p.Id.Value, StringComparer.Ordinal).ToList();
        var installed = new Installed(ordered.AsReadOnly(), ordered.ToDictionary(p => p.Id));
        if (Interlocked.CompareExchange(ref _installed, installed, null) is not null)
        {
            throw new InvalidOperationException("the provider registry is installed once per process");
        }
    }

    /// <summary>The module of an installed provider. An id with none is a host bug, thrown.</summary>
    public static IProviderModule Of(ProviderId id)
        => id.Value is not null && Current.ById.TryGetValue(id, out var provider)
            ? provider
            : throw new KeyNotFoundException($"no provider module declares the id '{id}'");

    /// <summary>
    /// [S-501] The provider an account file names, read leniently (surrounding space and case are
    /// forgiven, as the file has always been read), or false for one this build does not have.
    /// </summary>
    public static bool TryParse(string? wire, out ProviderId id)
    {
        var text = wire?.Trim();
        foreach (var provider in Current.All)
        {
            if (string.Equals(provider.Id.Value, text, StringComparison.OrdinalIgnoreCase))
            {
                id = provider.Id;
                return true;
            }
        }

        id = default;
        return false;
    }

    /// <summary>[S-502] What an account of <paramref name="id"/> feeds.</summary>
    public static FeatureSet FeaturesOf(ProviderId id) => Of(id).Features;

    private static Installed Current
        => Volatile.Read(ref _installed)
            ?? throw new InvalidOperationException("the provider registry is not installed: the host installs it before anything reads an account ([S-6910])");
}
