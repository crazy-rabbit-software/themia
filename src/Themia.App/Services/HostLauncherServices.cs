using Themia.App.Services.Web;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Services;

/// <summary>[S-1411] <see cref="IWidgetHost.AppLayouts"/> and <see cref="SettingsHost.AppLayouts"/>: the host's one editor window and the layout launch.</summary>
internal sealed class HostAppLayouts : IAppLayouts
{
    public static HostAppLayouts Instance { get; } = new();

    private HostAppLayouts()
    {
    }

    public bool CanEdit => AppServices.EditAppLayout is not null;

    public void Edit(string itemId) => AppServices.EditAppLayout?.Invoke(itemId);

    public void Launch(LayoutNode? layout, nint anchor) => AppLayoutLauncher.Launch(layout, anchor);
}

/// <summary>[S-471] <see cref="IWidgetHost.Streams"/>: one <see cref="StreamSession"/> per camera connection, on the UI thread's queue.</summary>
internal sealed class HostVideoStreams : IVideoStreams
{
    public static HostVideoStreams Instance { get; } = new();

    private HostVideoStreams()
    {
    }

    public IDisposable Open(Uri uri, int refreshSeconds, Func<DecodeTarget> target, IStreamSink sink)
    {
        var ui = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("a stream is opened on the UI thread");
        var session = new StreamSession(uri, refreshSeconds, target, sink, ui);
        session.Start();
        return session;
    }
}

/// <summary><see cref="IWidgetHost.InstalledApps"/>: the Start menu's apps.</summary>
internal sealed class HostInstalledApps : IInstalledApps
{
    public static HostInstalledApps Instance { get; } = new();

    private HostInstalledApps()
    {
    }

    public IReadOnlyList<InstalledApp> Read(int maxCount, CancellationToken token) => InstalledApps.Read(maxCount, token);
}

/// <summary>[S-1304] <see cref="IWidgetHost.Favicons"/>: the host's one site-icon cache.</summary>
internal sealed class HostFavicons : IFavicons
{
    public static HostFavicons Instance { get; } = new();

    private HostFavicons()
    {
    }

    public Task<byte[]?> GetAsync(string link) => FaviconCache.GetAsync(link);
}
