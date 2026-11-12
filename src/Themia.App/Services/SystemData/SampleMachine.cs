#if DEBUG
using Microsoft.UI.Dispatching;
using Themia.Core.SystemData;
using Themia.Sdk;

namespace Themia.App.Services.SystemData;

/// <summary>
/// [S-1931] Debug renders: the system widgets read the sample machine instead of this PC. One line
/// for any render, before its cards are built:
/// <code>using var machine = SampleMachine.Install(ticker, dispatcher);</code>
/// It puts a hub over the sample machine (<see cref="SamplePc"/>) in <see cref="AppServices.System"/>,
/// which every card's <c>IWidgetHost.System</c> is, so the meters, the details and the process list
/// draw it through their own code: the hub's rules, its series with a full history from the first
/// frame ([S-1932]), the widgets' views. Nothing is read from this PC and nothing moves (the hub does
/// not tick over a recording), so two renders draw the same picture. Dispose to put back what was
/// there (none, in a render). <c>--widget-png ... --machine sample</c> is this.
/// </summary>
public static class SampleMachine
{
    /// <summary>[S-1931] Install the sample machine's hub; dispose to take it away again.</summary>
    public static IDisposable Install(Ticker ticker, DispatcherQueue dispatcher)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        ArgumentNullException.ThrowIfNull(dispatcher);
        return new Installed(Hub(ticker, dispatcher));
    }

    /// <summary>
    /// [S-11710] A hub over the sample machine that is not installed: the add page's pictures hand it to
    /// the cards they build, so the app's own hub (this PC) stays every desktop card's. Dispose it.
    /// </summary>
    internal static SystemHub Hub(Ticker ticker, DispatcherQueue dispatcher)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        ArgumentNullException.ThrowIfNull(dispatcher);
        return new SystemHub(ticker, dispatcher, new Recording());
    }

    private sealed class Installed : IDisposable
    {
        private readonly SystemHub _hub;
        private readonly SystemHub? _before;
        private bool _disposed;

        public Installed(SystemHub hub)
        {
            _hub = hub;
            _before = AppServices.System;
            AppServices.System = hub;
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

            _hub.Dispose();
        }
    }

    /// <summary>The sample machine as the hub reads it: each source its recorded readings, the newest now.</summary>
    private sealed class Recording : SystemMachine
    {
        public override bool Live => false;

        public override HubSource<T> Open<T>(SourceId<T> source, string target) => new Recorded<T>(SamplePc.Readings(source, target));
    }

    /// <summary>One source's readings: the newest is every read, the rest its past ([S-1932]).</summary>
    private sealed class Recorded<T>(IReadOnlyList<T> readings) : HubSource<T>
    {
        public override IReadOnlyList<T> Past { get; } = readings.Count > 1 ? readings.Take(readings.Count - 1).ToArray() : [];

        public override T Read() => readings[^1];
    }
}
#endif
