using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services.SystemData;

internal sealed partial class SystemHub
{
    /// <summary>
    /// [S-6310] Each logical processor's load, for the CPU meter's All cores and Core grid styles:
    /// one PDH query (<c>\Processor Information(*)\% Processor Time</c>), opened when the first such
    /// widget wants it and closed with the feed ([S-2505]), read once per tick for all of them
    /// ([S-2501]). Empty when Windows has no such counter, which the styles draw as the reading alone.
    /// </summary>
    private sealed class CpuCoresSource : HubSource<IReadOnlyList<double?>>
    {
        private const string CoresScope = "cpu-cores";

        private PdhQuery? _query;
        private PdhCounter? _counter;

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

        public override IReadOnlyList<double?> Read()
        {
            if (_query is not { } query || _counter is not { } counter)
            {
                return [];
            }

            query.Collect();
            return CpuCoreRules.Loads(query.Doubles(counter));
        }

        public override void Close()
        {
            _query?.Dispose();
            _query = null;
            _counter = null;
        }
    }
}
