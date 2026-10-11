using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-2430] [S-2432] What the GPU details widget adds to the shared sampler: each adapter's memory in
/// use (<c>\GPU Adapter Memory(*)\Dedicated Usage</c> and <c>Shared Usage</c>, in the same query as
/// the engines, so one collection serves both) and its sensors from the graphics kernel
/// (<c>D3DKMTQueryAdapterInfo</c>: the temperature, fan and clocks Task Manager shows, from any
/// vendor's WDDM 2.4+ driver, without a vendor library or admin rights).
/// </summary>
internal static partial class GpuSampler
{
    /// <summary>
    /// The graphics kernel's per-adapter performance data. One handle per adapter, opened from its
    /// LUID when the session opens and closed with it; an adapter or driver that refuses a query
    /// simply has no sensors, and that is logged once.
    /// </summary>
    private sealed unsafe partial class GpuKernel : IDisposable
    {
        /// <summary>KMTQAITYPE_NODEPERFDATA, _ADAPTERPERFDATA, _ADAPTERPERFDATA_CAPS.</summary>
        private const int NodePerfData = 61;
        private const int AdapterPerfData = 62;
        private const int AdapterPerfDataCaps = 63;

        private readonly Dictionary<string, uint> _handles = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, byte[]> _caps = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _refused = new(StringComparer.OrdinalIgnoreCase);
        private bool _disposed;

        public GpuKernel(IReadOnlyList<GpuAdapter> adapters)
        {
            foreach (var adapter in adapters)
            {
                var open = new OpenAdapterFromLuid { LuidLow = adapter.LuidLow, LuidHigh = adapter.LuidHigh };
                var status = D3DKMTOpenAdapterFromLuid(&open);

                if (status != 0 || open.Adapter == 0)
                {
                    Log.Info(Scope, $"the graphics kernel would not open {adapter.Name} (0x{status:X8}); no sensors for it");
                    continue;
                }

                _handles[adapter.Luid] = open.Adapter;

                var caps = new byte[GpuDetailRules.AdapterCapsSize];
                _caps[adapter.Luid] = Query(open.Adapter, AdapterPerfDataCaps, caps) ? caps : [];
            }
        }

        /// <summary>[S-2432] The sensors of the adapter filed under <paramref name="luid"/>; all null when it has none.</summary>
        public GpuSensors Read(string luid)
        {
            if (_disposed || !_handles.TryGetValue(luid, out var handle) || _refused.Contains(luid))
            {
                return default;
            }

            // The query's input names the physical adapter (0) and, for a node, the engine (0 is 3D).
            var adapter = new byte[GpuDetailRules.AdapterPerfSize];
            var node = new byte[GpuDetailRules.NodePerfSize];

            var haveAdapter = Query(handle, AdapterPerfData, adapter);
            var haveNode = Query(handle, NodePerfData, node);

            if (!haveAdapter && !haveNode)
            {
                // An older driver model: asking every sample would fail every sample.
                _refused.Add(luid);
                Log.Info(Scope, "the graphics driver reports no performance data; the GPU details show no sensors");
                return default;
            }

            return GpuDetailRules.ParseSensors(haveAdapter ? adapter : [], haveNode ? node : [], _caps.GetValueOrDefault(luid) ?? []);
        }

        private static bool Query(uint adapter, int type, byte[] data)
        {
            fixed (byte* buffer = data)
            {
                var query = new QueryAdapterInfo
                {
                    Adapter = adapter,
                    Type = type,
                    PrivateData = buffer,
                    PrivateDataSize = (uint)data.Length,
                };

                return D3DKMTQueryAdapterInfo(&query) == 0;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (var handle in _handles.Values)
            {
                var close = new CloseAdapter { Adapter = handle };
                _ = D3DKMTCloseAdapter(&close); // ignore: closing at teardown; nothing left to do on failure
            }

            _handles.Clear();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct OpenAdapterFromLuid
        {
            public uint LuidLow;
            public int LuidHigh;
            public uint Adapter;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct QueryAdapterInfo
        {
            public uint Adapter;
            public int Type;
            public byte* PrivateData;
            public uint PrivateDataSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CloseAdapter
        {
            public uint Adapter;
        }

        [LibraryImport("gdi32.dll")]
        private static partial int D3DKMTOpenAdapterFromLuid(OpenAdapterFromLuid* open);

        [LibraryImport("gdi32.dll")]
        private static partial int D3DKMTQueryAdapterInfo(QueryAdapterInfo* query);

        [LibraryImport("gdi32.dll")]
        private static partial int D3DKMTCloseAdapter(CloseAdapter* close);
    }
}
