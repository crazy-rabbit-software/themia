#if DEBUG
using Microsoft.UI.Dispatching;
using Themia.Core.SystemData;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.App.Services.SystemData;

/// <summary>
/// [S-1931] Debug renders: the system widgets read the sample machine instead of this PC. One line
/// for any render, before its cards are built:
/// <code>using var machine = SampleMachine.Install(ticker, dispatcher);</code>
/// It puts a hub over the sample machine (<see cref="SamplePc"/>) in <see cref="AppServices.System"/>,
/// which every card's <c>IWidgetHost.System</c> is, so the meters, the details and the process list
/// draw it through their own code: the hub's rules, its series with a full history from the first
/// frame ([S-1932]), the widgets' views. Nothing is read from this PC and nothing moves (the hub does
/// not tick over a recording), so two renders draw the same picture. [S-11802] The drive widgets read
/// its drives (<see cref="SamplePc.Drives"/>) the same way, through <see cref="AppServices.Drives"/>.
/// Dispose to put back what was there (none, in a render). <c>--widget-png ... --machine sample</c> is this.
/// </summary>
public static class SampleMachine
{
    /// <summary>[S-1931] Install the sample machine's hub; dispose to take it away again.</summary>
    public static IDisposable Install(Ticker ticker, DispatcherQueue dispatcher)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        ArgumentNullException.ThrowIfNull(dispatcher);
        return new Installed(Hub(ticker, dispatcher), new SampleDrives());
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
        private readonly IDrives _drivesBefore;
        private bool _disposed;

        public Installed(SystemHub hub, IDrives drives)
        {
            _hub = hub;
            _before = AppServices.System;
            AppServices.System = hub;
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

    /// <summary>[S-11802] The sample machine's drives: one fixed walk, nothing watched, nothing walked again.</summary>
    private sealed class SampleDrives : IDrives
    {
        public IReadOnlyList<Themia.Core.Widgets.DriveEntry> Drives => SamplePc.Drives;

        public bool HasValue => true;

        public IDisposable Watch(Ticker ticker, Action onChanged) => new Nothing();

        public void Refresh()
        {
            // ignore: the sample machine's drives never change.
        }

        private sealed class Nothing : IDisposable
        {
            public void Dispose()
            {
                // ignore: nothing is watched.
            }
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
