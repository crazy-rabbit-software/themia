using Themia.App.Shell;
using Themia.Core.Services;
using Themia.Sdk.Services;
using Themia.Sdk.WinUI;

namespace Themia.App.Services.Kit;

/// <summary>
/// [S-9925] [S-6913] The service sources a card's view reads: the host's one <see cref="ServiceSync"/>
/// on the desktop; on a preview card the providers' recorded answers in a Debug build ([S-9929]) and,
/// in any other, a sync with no accounts and no network: a picture's card never reads the user's
/// accounts ([S-11851]); where no sync runs, the same.
/// </summary>
internal static class HostServiceSources
{
    public static IServiceSources For(IWidgetCardHost card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card.IsPreview)
        {
#if DEBUG
            return Shell.Gallery.FixtureServices.For(card);
#else
            return new Detached(card.Ticker);
#endif
        }

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
