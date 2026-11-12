using Microsoft.UI.Dispatching;
using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Sdk.WinUI;
using Themia.Sdk;

namespace Themia.App.Shell;

/// <summary>
/// [S-11851] What a picture's cards read in place of this PC's: the sample PC's hub ([S-1931]) and a
/// gallery service of their own (so the sample pictures decode where the app's is not running, and never
/// land in the desktop's cache). Handed explicitly to the card host that builds the pictures
/// (<see cref="PreviewCardHost.Samples"/>), whose cards' <see cref="CardWidgetHost"/> reads it; no static
/// is swapped, so a desktop card, whose host has none, can never see it. One per add page, disposed with it.
/// </summary>
internal sealed class SampleWorld : IDisposable
{
    public SampleWorld(Ticker ticker, DispatcherQueue dispatcher)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        ArgumentNullException.ThrowIfNull(dispatcher);
        System = SampleMachine.Hub(ticker, dispatcher);
        Gallery = new GalleryService(ticker, dispatcher);
    }

    /// <summary>[S-1931] The sample PC's hub.</summary>
    public SystemHub System { get; }

    /// <summary>The pictures' own gallery service.</summary>
    public GalleryService Gallery { get; }

    /// <summary>[S-11802] The sample PC's drives: a picture's drive widgets show them, never this PC's.</summary>
    public IDrives Drives { get; } = SampleMachine.Drives();

    public void Dispose()
    {
        System.Dispose();
        Gallery.Dispose();
    }
}
