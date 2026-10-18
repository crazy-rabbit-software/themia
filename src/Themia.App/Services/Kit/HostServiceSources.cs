using Themia.App.Shell;
using Themia.Core.Services;
using Themia.Sdk.Services;
using Themia.Sdk.WinUI;

namespace Themia.App.Services.Kit;

/// <summary>
/// [S-9925] [S-6913] The service sources a card's view reads: the host's one <see cref="ServiceSync"/>
/// on the desktop; on a preview card (a debug render) the providers' recorded answers
/// ([S-9929]); where neither runs, a sync with no accounts and no network that holds nothing.
/// </summary>
internal static class HostServiceSources
{
    public static IServiceSources For(IWidgetCardHost card)
    {
        ArgumentNullException.ThrowIfNull(card);
#if DEBUG
        if (card.IsPreview)
        {
            return Shell.Gallery.FixtureServices.For(card.Ticker);
        }
#endif
        return AppServices.Services is { } sync ? sync : new Detached(card.Ticker);
    }

    /// <summary>No sync is running: every source loads forever and asks nothing.</summary>
    private sealed class Detached(Ticker ticker) : IServiceSources
    {
        public IServiceSource Open(ServiceInterest interest)
            => new ServiceSync(ticker, new NoNetwork(), static () => []).Open(interest);
    }

    private sealed class NoNetwork : IServiceTransport
    {
        public Task<ServiceReply> SendAsync(string? accountId, ServiceDescriptor descriptor, ServiceRequest request, string? ifNoneMatch, CancellationToken cancellation)
            => throw new Core.Accounts.AccountException(Core.Accounts.AccountError.Offline, "no service sync is running here");
    }
}
