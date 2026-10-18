using System.Diagnostics;
using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-431] Graphics load and memory for every adapter, sampled once for however many GPU widgets
/// are on the desktop.
/// <para>
/// <b>Where the numbers come from.</b> The original read adapter names and memory sizes from the
/// display class key in the registry, and load and memory use from the GPU performance counters
/// through PDH. The counters are kept — they are the only system-wide source, they are what Task
/// Manager reads, and PDH is a plain C API that <c>LibraryImport</c> binds without any runtime
/// marshalling, so it is AOT-clean. The registry is replaced by DXGI: its numbered subkeys include
/// adapters that are no longer present and stop at the first gap, while DXGI lists exactly the
/// adapters there are and hands over each one's LUID — the key the counters are filed under — so
/// nothing has to be parsed back out of a registry blob. DXGI is two COM calls through a vtable,
/// the same pattern as <see cref="CoreAudio"/>. Not WMI, which Native AOT cannot use, and not
/// <c>PerformanceCounter</c>, which is not trim-safe.
/// </para>
/// <para>
/// <b>NVIDIA.</b> When the driver's management library is present it is asked too ([S-435]): its
/// load figure is the true one for a borderless full-screen game, where the 3D counter reads near
/// zero. It is
/// loaded with <see cref="NativeLibrary"/> and called through function pointers, so a machine
/// without it costs one failed <c>LoadLibrary</c> and nothing else.
/// </para>
/// <para>
/// <b>Shared and lazy.</b> One query, opened when the system hub's GPU source opens and closed when
/// it closes ([S-2505]). The hub asks once per due tick for every GPU widget ([S-2501]); a request
/// while one is being taken waits for it rather than starting another. Sampling runs on the thread
/// pool: a counter array of a few hundred engine instances is not something to walk on the UI thread.
/// </para>
/// </summary>
internal static partial class GpuSampler
{
    private const string Scope = "gpu";

    private static readonly Lock Gate = new();

    private static int _users;
    private static Session? _session;
    private static Task<GpuSnapshot>? _inFlight;

    /// <summary>Start sharing the sampler. Pair every call with <see cref="Release"/>.</summary>
    public static void Acquire()
    {
        lock (Gate)
        {
            _users++;
        }
    }

    /// <summary>
    /// Stop sharing it. The last widget to leave closes the counters and lets go of the driver
    /// library, on the pool, after any sample still being taken has finished.
    /// </summary>
    public static void Release()
    {
        Session? closing = null;
        Task? pending;

        lock (Gate)
        {
            _users = Math.Max(0, _users - 1);

            if (_users > 0)
            {
                return;
            }

            closing = _session;
            pending = _inFlight;
            _session = null;
            _inFlight = null;
        }

        if (closing is null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            if (pending is not null)
            {
                try
                {
                    await pending.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // ignore: a failed last sample has already been logged by the sample itself
                    Log.Debug(Scope, $"last sample before closing failed: {ex.Message}");
                }
            }

            closing.Dispose();
        });
    }

    /// <summary>
    /// A sample of every adapter: the one being taken when there is one, otherwise a new one. Never
    /// faults: a failure is logged and reads as no adapters.
    /// </summary>
    public static Task<GpuSnapshot> SampleAsync()
    {
        lock (Gate)
        {
            if (_inFlight is not null)
            {
                return _inFlight;
            }

            _session ??= new Session();
            var session = _session;
            var task = Task.Run(() => Take(session));
            _inFlight = task;
            return task;
        }
    }

    private static GpuSnapshot Take(Session session)
    {
        GpuSnapshot snapshot;

        try
        {
            snapshot = session.Sample();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "could not sample the graphics adapters", ex);
            snapshot = new GpuSnapshot([], [], false);
        }

        lock (Gate)
        {
            if (ReferenceEquals(_session, session))
            {
                _inFlight = null;
            }
        }

        return snapshot;
    }

    /// <summary>The adapters on this machine, for the settings picker. Cheap; safe on any thread.</summary>
    public static IReadOnlyList<GpuAdapter> Adapters() => Dxgi.Enumerate();

    /// <summary>
    /// The open counters and driver library. Used by one sample at a time — <see cref="SampleAsync"/>
    /// never runs two — and disposed only once no sample can still be using it.
    /// </summary>
    private sealed class Session : IDisposable
    {
        private readonly IReadOnlyList<GpuAdapter> _adapters;
        private readonly GpuCounters? _counters;
        private readonly Nvml? _nvml;
        private readonly GpuKernel _kernel;
        private bool _baselined;

        public Session()
        {
            _adapters = Dxgi.Enumerate();
            _counters = GpuCounters.Open();
            _nvml = Nvml.TryOpen();
            _kernel = new GpuKernel(_adapters);
        }

        public GpuSnapshot Sample()
        {
            var engines = _counters?.Engines() ?? [];
            var dedicated = _counters?.Dedicated() ?? [];
            var shared = _counters?.Shared() ?? [];

            // The query collected once when it opened, so every sample after the first has a
            // second reading to make a rate from.
            var baselined = _baselined;
            _baselined = true;
            var anyLoad = false;

            var readings = new List<GpuReading>(_adapters.Count);
            var details = new List<GpuDetail>(_adapters.Count);
            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var adapter in _adapters)
            {
                var ordinal = seen.GetValueOrDefault(adapter.Name);
                seen[adapter.Name] = ordinal + 1;

                var driver = _nvml?.Read(adapter.Name, ordinal);

                var counters = GpuRules.Utilization(engines, adapter.Luid);

                readings.Add(new GpuReading(adapter.Name, GpuRules.ChooseUtilization(driver, counters))
                {
                    // The driver's load needs no baseline, so an NVIDIA card has a real figure at once.
                    HasLoad = baselined || driver is not null,

                    // [S-6322] The engines are always the counters' (the driver gives one figure only).
                    Engines = baselined ? GpuRules.EngineLoads(engines, adapter.Luid) : [],
                });

                anyLoad |= readings[^1].HasLoad;

                details.Add(new GpuDetail(
                    adapter.Name,
                    readings[^1].HasLoad ? readings[^1].Utilization : null,
                    GpuDetailRules.SumFor(dedicated, adapter.Luid),
                    adapter.DedicatedBytes,
                    GpuDetailRules.SumFor(shared, adapter.Luid),
                    adapter.SharedBytes,
                    _kernel.Read(adapter.Luid)));
            }

            return new GpuSnapshot(_adapters, readings, anyLoad) { Details = details };
        }

        public void Dispose()
        {
            _counters?.Dispose();
            _nvml?.Dispose();
            _kernel.Dispose();
        }
    }

    // ---------------------------------------------------------------------------------------------
    // DXGI: which adapters there are
    // ---------------------------------------------------------------------------------------------

    private static unsafe partial class Dxgi
    {
        /// <summary>IID_IDXGIFactory1.</summary>
        private static readonly Guid FactoryInterface = new("770AAE78-F26F-4DBA-A829-253C83D1B387");

        private const int SlotRelease = 2;
        private const int SlotEnumAdapters1 = 12;
        private const int SlotGetDesc1 = 10;

        /// <summary>DXGI_ERROR_NOT_FOUND: the end of the list.</summary>
        private const int NotFound = unchecked((int)0x887A0002);

        /// <summary>DXGI_ADAPTER_FLAG_SOFTWARE: the Basic Render Driver, which has no counters worth drawing.</summary>
        private const uint SoftwareAdapter = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct AdapterDesc1
        {
            public fixed char Description[128];
            public uint VendorId;
            public uint DeviceId;
            public uint SubSysId;
            public uint Revision;
            public nuint DedicatedVideoMemory;
            public nuint DedicatedSystemMemory;
            public nuint SharedSystemMemory;
            public uint LuidLow;
            public int LuidHigh;
            public uint Flags;
        }

        public static IReadOnlyList<GpuAdapter> Enumerate()
        {
            var adapters = new List<GpuAdapter>();
            nint factory = 0;

            try
            {
                var iid = FactoryInterface;
                var created = CreateDXGIFactory1(&iid, &factory);

                if (created < 0)
                {
                    Log.Warn(Scope, $"could not open DXGI: 0x{created:X8}");
                    return adapters;
                }

                for (uint i = 0; ; i++)
                {
                    nint adapter = 0;
                    var found = ((delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)Slot(factory, SlotEnumAdapters1))(factory, i, &adapter);

                    if (found == NotFound)
                    {
                        break;
                    }

                    if (found < 0)
                    {
                        Log.Warn(Scope, $"could not list adapter {i}: 0x{found:X8}");
                        break;
                    }

                    try
                    {
                        AdapterDesc1 desc;
                        var described = ((delegate* unmanaged[Stdcall]<nint, AdapterDesc1*, int>)Slot(adapter, SlotGetDesc1))(adapter, &desc);

                        if (described < 0 || (desc.Flags & SoftwareAdapter) != 0)
                        {
                            continue;
                        }

                        var name = new string(desc.Description).TrimEnd('\0').Trim();

                        adapters.Add(new GpuAdapter(
                            adapters.Count,
                            name.Length > 0 ? name : "GPU",
                            GpuRules.LuidKey(unchecked((uint)desc.LuidHigh), desc.LuidLow),
                            desc.DedicatedVideoMemory,
                            desc.SharedSystemMemory,
                            desc.LuidLow,
                            desc.LuidHigh));
                    }
                    finally
                    {
                        Release(adapter);
                    }
                }
            }
            finally
            {
                Release(factory);
            }

            return adapters;
        }

        private static void Release(nint com)
        {
            if (com != 0)
            {
                ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(com, SlotRelease))(com);
            }
        }

        private static nint Slot(nint com, int index) => (*(nint**)com)[index];

        [LibraryImport("dxgi.dll")]
        private static partial int CreateDXGIFactory1(Guid* riid, nint* factory);
    }

    // ---------------------------------------------------------------------------------------------
    // PDH: what the adapters are doing
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The GPU engine counters in one shared <see cref="PdhQuery"/>, which owns the query handle and
    /// closes it exactly once; the counter belongs to the query and goes with it.
    /// </summary>
    private sealed class GpuCounters : IDisposable
    {
        private readonly PdhQuery _query;
        private readonly PdhCounter _engines;
        private readonly PdhCounter? _dedicated;
        private readonly PdhCounter? _shared;

        private GpuCounters(PdhQuery query, PdhCounter engines, PdhCounter? dedicated, PdhCounter? shared)
        {
            _query = query;
            _engines = engines;
            _dedicated = dedicated;
            _shared = shared;
        }

        /// <summary>The counters, collected once so the next collection has something to rate against. Null when PDH refuses.</summary>
        public static GpuCounters? Open()
        {
            if (PdhQuery.Open(Scope) is not { } query)
            {
                return null;
            }

            if (query.Add(@"\GPU Engine(*)\Utilization Percentage") is not { } engines)
            {
                // No engine counters means no GPU counters at all — an old driver model or a VM.
                Log.Warn(Scope, "this machine has no GPU engine counters");
                query.Dispose();
                return null;
            }

            // [S-2430] Memory in use per adapter, collected with the engines; optional, like any
            // counter a machine may lack.
            var counters = new GpuCounters(query, engines,
                query.Add(@"\GPU Adapter Memory(*)\Dedicated Usage"),
                query.Add(@"\GPU Adapter Memory(*)\Shared Usage"));

            query.Collect();
            return counters;
        }

        /// <summary>Per-instance engine load, after collecting the query for this sample.</summary>
        public List<(string Instance, double Percent)> Engines()
        {
            // One collection per sample.
            _query.Collect();
            return _query.Doubles(_engines);
        }

        /// <summary>[S-2430] Dedicated memory in use per instance, from the collection <see cref="Engines"/> just made.</summary>
        public List<(string Instance, long Value)> Dedicated() => _dedicated is { } c ? _query.Larges(c) : [];

        /// <summary>[S-2430] Shared memory in use per instance, from the same collection.</summary>
        public List<(string Instance, long Value)> Shared() => _shared is { } c ? _query.Larges(c) : [];

        public void Dispose() => _query.Dispose();
    }

    // ---------------------------------------------------------------------------------------------
    // NVML: what only an NVIDIA driver will say
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// [S-435] The NVIDIA management library, if this machine has it: the true load of a card in a
    /// borderless full-screen game. Initialised once per session and shut down with it; a failure at
    /// any point leaves the counters' figure.
    /// </summary>
    private sealed unsafe class Nvml : IDisposable
    {
        private const int Success = 0;

        private readonly nint _library;
        private readonly delegate* unmanaged<int> _shutdown;
        private readonly delegate* unmanaged<uint*, int> _count;
        private readonly delegate* unmanaged<uint, nint*, int> _handle;
        private readonly delegate* unmanaged<nint, byte*, uint, int> _name;
        private readonly delegate* unmanaged<nint, uint*, int> _utilization;
        private bool _disposed;

        private Nvml(nint library)
        {
            _library = library;
            _shutdown = (delegate* unmanaged<int>)NativeLibrary.GetExport(library, "nvmlShutdown");
            _count = (delegate* unmanaged<uint*, int>)NativeLibrary.GetExport(library, "nvmlDeviceGetCount_v2");
            _handle = (delegate* unmanaged<uint, nint*, int>)NativeLibrary.GetExport(library, "nvmlDeviceGetHandleByIndex_v2");
            _name = (delegate* unmanaged<nint, byte*, uint, int>)NativeLibrary.GetExport(library, "nvmlDeviceGetName");
            _utilization = (delegate* unmanaged<nint, uint*, int>)NativeLibrary.GetExport(library, "nvmlDeviceGetUtilizationRates");
        }

        /// <summary>The library, initialised, or null on any machine without a working NVIDIA driver.</summary>
        public static Nvml? TryOpen()
        {
            // System32 is where every current driver installs it; the NVSMI folder is the old home.
            var system = Path.Combine(Environment.SystemDirectory, "nvml.dll");
            var legacy = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "NVIDIA Corporation", "NVSMI", "nvml.dll");

            if (!NativeLibrary.TryLoad(system, out var library) && !NativeLibrary.TryLoad(legacy, out library))
            {
                return null;
            }

            try
            {
                var init = (delegate* unmanaged<int>)NativeLibrary.GetExport(library, "nvmlInit_v2");
                var status = init();

                if (status != Success)
                {
                    Log.Info(Scope, $"the NVIDIA driver library would not start (status {status}); using the counters only");
                    NativeLibrary.Free(library);
                    return null;
                }

                return new Nvml(library);
            }
            catch (EntryPointNotFoundException ex)
            {
                Log.Warn(Scope, "the NVIDIA driver library is missing an entry point; using the counters only", ex);
                NativeLibrary.Free(library);
                return null;
            }
        }

        /// <summary>
        /// The load of the card DXGI calls <paramref name="name"/>, 0..100, or null when it is not an
        /// NVIDIA card, cannot be told apart, or the call failed. Matched by name; <paramref name="ordinal"/> — how many adapters of the same
        /// name came before it — picks between identical cards.
        /// </summary>
        public double? Read(string name, int ordinal)
        {
            if (_disposed || !name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (Device(name, ordinal) is not { } device)
            {
                return null;
            }

            var utilization = stackalloc uint[2];
            return _utilization(device, utilization) == Success ? utilization[0] : null;
        }

        private nint? Device(string name, int ordinal)
        {
            uint count;
            if (_count(&count) != Success || count == 0)
            {
                return null;
            }

            nint? only = null;
            var sameName = 0;
            var name8 = stackalloc byte[96];

            for (uint i = 0; i < count; i++)
            {
                nint handle;
                if (_handle(i, &handle) != Success)
                {
                    continue;
                }

                only = count == 1 ? handle : null;

                if (_name(handle, name8, 96) != Success)
                {
                    continue;
                }

                var deviceName = Marshal.PtrToStringUTF8((nint)name8) ?? string.Empty;

                // Two identical cards: the n-th DXGI one of that name is the n-th driver one.
                if (string.Equals(deviceName.Trim(), name, StringComparison.OrdinalIgnoreCase) && sameName++ == ordinal)
                {
                    return handle;
                }
            }

            // No name matched. With a single card there is no way to pick the wrong one; with
            // several, a guess would put one card's load under another card's name.
            return only;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _shutdown();
            NativeLibrary.Free(_library);
        }
    }
}
