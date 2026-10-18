using Themia.Sdk.Kit;
using Themia.Sdk.Services;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-9925] [S-6913] Where every kit service widget reads: the host's one service sync, the only code
/// that asks a kit service for anything ([S-2600]). A view opens a source for its config's interest
/// (its provider, query or replica view, accounts and count) and hands it to its <c>KitBinding</c>;
/// the source never fetches from a tick of its own. On a preview card (a debug render) the source
/// replays the provider's recorded answers instead ([S-9929]).
/// </summary>
public interface IServiceSources
{
    /// <summary>A kit source over the sync for one widget. Dispose it when the view detaches.</summary>
    IServiceSource Open(ServiceInterest interest);
}

/// <summary>[S-9925] One widget's view of the service sync.</summary>
public interface IServiceSource : IKitSource, IDisposable
{
    /// <summary>The widget's settings changed what it reads.</summary>
    void Retarget(ServiceInterest interest);
}
