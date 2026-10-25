using Themia.Core.Widgets;

namespace Themia.Sdk;

/// <summary>
/// [S-1307] The symbol search a Stocks widget's settings ask (the request is the host's market service,
/// the row is the widget's), installed by the host once at start, as the place lookup is.
/// </summary>
public static class SymbolLookup
{
    private static Func<string, CancellationToken, Task<IReadOnlyList<SymbolHit>>>? _search;

    /// <summary>The instruments a query names, best first; empty for a query too short to ask.</summary>
    public static Task<IReadOnlyList<SymbolHit>> FindAsync(string query, CancellationToken token)
        => (Volatile.Read(ref _search) ?? throw new InvalidOperationException("no symbol search installed: the host installs it before any menu is shown"))(query, token);

    /// <summary>The host's market search. Once per process.</summary>
    internal static void Install(Func<string, CancellationToken, Task<IReadOnlyList<SymbolHit>>> search)
    {
        ArgumentNullException.ThrowIfNull(search);
        if (Interlocked.CompareExchange(ref _search, search, null) is not null)
        {
            throw new InvalidOperationException("the symbol search is installed once per process");
        }
    }
}
