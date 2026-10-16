using Themia.Core.Diagnostics;
using Themia.Core.SystemData;
using Themia.Core.Widgets;

namespace Themia.App.Services.SystemData;

internal sealed partial class SystemHub
{
    /// <summary>
    /// [S-6310] Each logical processor's load, for the CPU meter's All cores and Core grid styles:
    /// one PDH query (<c>\Processor Information(*)\% Processor Time</c>), opened when the first such
    /// widget wants it and closed with the feed ([S-2505]), read once per tick for all of them
    /// ([S-2501]). Empty when Windows has no such counter, which the styles draw as the reading alone.
    /// <para>
    /// [S-6318] Every read is a sample (a rate counter rates since the last collection), so each one
    /// is added to every core's history, which holds the longest Core graphs span the views want
    /// (<see cref="CoreHistory.CapacityFor"/>) and nothing while no widget shows the style.
    /// </para>
    /// </summary>
    private sealed class CpuCoresSource : HubSource<CoreLoads>
    {
        private const string CoresScope = "cpu-cores";

        private readonly CoreHistory _history = new();
        private PdhQuery? _query;
        private PdhCounter? _counter;

        public override void Want(SystemInterests interests)
            => _history.Resize(CoreHistory.CapacityFor(interests.CoreHistorySeconds));

        public override void Open()
        {
            _query = PdhQuery.Open(CoresScope);

            if (_query is null)
            {
                // Logged by Open; every read is then empty.
                return;
            }

            _counter = _query.Add(@"\Processor Information(*)\% Processor Time");

            if (_counter is null)
            {
                Log.Warn(CoresScope, "this machine reports no per-processor counter; the core styles show the reading alone");
            }

            // A rate counter needs a first collection to have something to rate against.
            _query.Collect();
        }

        public override CoreLoads Read()
        {
            if (_query is not { } query || _counter is not { } counter)
            {
                return new CoreLoads([], _history);
            }

            query.Collect();
            var loads = CpuCoreRules.Loads(query.Doubles(counter));
            _history.Add(loads);
            return new CoreLoads(loads, _history);
        }

        public override void Close()
        {
            _query?.Dispose();
            _query = null;
            _counter = null;
            _history.Resize(0);
        }
    }
}
