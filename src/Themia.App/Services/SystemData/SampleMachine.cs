using Microsoft.UI.Dispatching;
using Themia.Core.SystemData;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.App.Services.SystemData;

/// <summary>
/// [S-1931] [S-11851] The sample machine (<see cref="SamplePc"/>) as a hub: the system widgets read it
/// instead of this PC through their own code (the hub's rules, its series with a full history from the
/// first frame, [S-1932], the widgets' views). Nothing is read from this PC and nothing moves (the hub
/// does not tick over a recording), so two pictures draw the same. In every build: the add page's
/// pictures hand it to the cards they build, and only to them (a <see cref="Shell.SampleWorld"/>, a
/// preview card host's own); it is never installed where a desktop card reads its hub. Debug renders
/// install one for a whole render (<c>--machine sample</c>, <c>RenderMachine.Install</c>).
/// </summary>
public static class SampleMachine
{
    /// <summary>[S-11851] A hub over the sample machine, for the cards that are handed it alone. Dispose it.</summary>
    internal static SystemHub Hub(Ticker ticker, DispatcherQueue dispatcher)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        ArgumentNullException.ThrowIfNull(dispatcher);
        return new SystemHub(ticker, dispatcher, new Recording());
    }

    /// <summary>[S-11802] The sample machine's drives (<see cref="SamplePc.Drives"/>), for whoever is handed the sample machine.</summary>
    internal static IDrives Drives() => new SampleDrives();

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
