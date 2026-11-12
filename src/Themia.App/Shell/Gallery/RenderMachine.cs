#if DEBUG
using Microsoft.UI.Dispatching;
using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-1931] Debug renders: the system widgets of a whole render read the sample machine instead of this
/// PC. One line for any render, before its cards are built:
/// <code>using var machine = RenderMachine.Install(ticker, dispatcher);</code>
/// It puts a hub over the sample machine in <see cref="AppServices.System"/>, which a render's cards read;
/// dispose to put back what was there (none, in a render). Render tooling only: a render runs instead
/// of the app, so no desktop card can see it. The add page's pictures never install it: they hand
/// their cards a sample world of their own ([S-11851]).
/// </summary>
internal static class RenderMachine
{
    /// <summary>[S-1931] Install the sample machine's hub; dispose to take it away again.</summary>
    public static IDisposable Install(Ticker ticker, DispatcherQueue dispatcher)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        ArgumentNullException.ThrowIfNull(dispatcher);
        return new Installed(SampleMachine.Hub(ticker, dispatcher), SampleMachine.Drives());
    }

    private sealed class Installed : IDisposable
    {
        private readonly SystemHub _hub;
        private readonly SystemHub? _before;
        private readonly IDrives _drivesBefore;
        private bool _disposed;

        public Installed(SystemHub hub, IDrives drives)
        {
            _hub = hub;
            _before = AppServices.System;
            AppServices.System = hub;

            // [S-11802] The drive widgets read the sample machine's drives the same way.
            _drivesBefore = AppServices.Drives;
            AppServices.Drives = drives;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (ReferenceEquals(AppServices.System, _hub))
            {
                AppServices.System = _before;
            }

            AppServices.Drives = _drivesBefore;

            _hub.Dispose();
        }
    }

}
#endif
