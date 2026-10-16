using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;

namespace Themia.App.Services;

/// <summary>
/// The COM plumbing under <see cref="CoreAudio"/>: vtable slots, interface ids, the immortal callback
/// objects and the handful of helpers every call goes through.
/// <para>
/// Hand-rolled vtable calls rather than a COM interop assembly: under Native AOT there is no runtime
/// IL emission, so a built-in COM interop type would need a marshalling stub that cannot be
/// generated. Each call is a single indirect call through the object's vtable — which is all a COM
/// call has ever been.
/// </para>
/// </summary>
internal static unsafe partial class CoreAudio
{
    private const uint ClsCtxInprocServer = 0x1;
    private const uint CoInitMultithreaded = 0x0;
    private const int DataFlowRender = 0;

    /// <summary>eConsole: the device the volume keys, the flyout and Sound settings' "default" act on.</summary>
    private const int RoleConsole = 0;

    private const uint DeviceStateActive = 0x1;
    private const uint StgmRead = 0;
    private const ushort VtLpwstr = 31;
    private const uint DeviceFriendlyNameId = 14;

    /// <summary>A PROPVARIANT is 16 bytes on x86 and 24 on x64; the buffer covers both.</summary>
    private const int PropVariantSize = 32;

    private const int SessionStateActive = 1;
    private const int SessionStateExpired = 2;
    private const int NotFound = unchecked((int)0x80070490);
    private const int NoInterface = unchecked((int)0x80004002);

    /// <summary>Vtable slots. Named, because an off-by-one calls a different method with these arguments.</summary>
    private static class Slots
    {
        public const int QueryInterface = 0;
        public const int AddRef = 1;
        public const int Release = 2;

        public const int EnumeratorEnumEndpoints = 3;
        public const int EnumeratorGetDefault = 4;
        public const int EnumeratorGetDevice = 5;
        public const int EnumeratorRegisterClient = 6;
        public const int EnumeratorUnregisterClient = 7;

        public const int CollectionGetCount = 3;
        public const int CollectionItem = 4;

        public const int DeviceActivate = 3;
        public const int DeviceOpenPropertyStore = 4;
        public const int DeviceGetId = 5;

        public const int StoreGetValue = 5;

        public const int ManagerGetSessionEnumerator = 5;
        public const int ManagerRegisterSessionNotification = 6;
        public const int ManagerUnregisterSessionNotification = 7;

        public const int ListGetCount = 3;
        public const int ListGetSession = 4;

        public const int ControlGetState = 3;
        public const int ControlGetDisplayName = 4;
        public const int ControlRegisterNotification = 10;
        public const int ControlUnregisterNotification = 11;

        public const int Control2GetInstanceIdentifier = 13;
        public const int Control2GetProcessId = 14;
        public const int Control2IsSystemSounds = 15;

        public const int SimpleSetMasterVolume = 3;
        public const int SimpleGetMasterVolume = 4;
        public const int SimpleSetMute = 5;
        public const int SimpleGetMute = 6;

        public const int EndpointRegisterNotify = 3;
        public const int EndpointUnregisterNotify = 4;
        public const int EndpointSetScalar = 7;
        public const int EndpointGetScalar = 9;
        public const int EndpointSetMute = 14;
        public const int EndpointGetMute = 15;
    }

    private static class Iids
    {
        public static readonly Guid DeviceEnumeratorClass = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
        public static readonly Guid DeviceEnumerator = new("A95664D2-9614-4F35-A746-DE8DB63617E6");
        public static readonly Guid SessionManager2 = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
        public static readonly Guid SessionControl2 = new("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D");
        public static readonly Guid SimpleAudioVolume = new("87CE5498-68D6-44E5-9215-6DA47EF883D8");
        public static readonly Guid EndpointVolume = new("5CDF2C82-841E-4546-9722-0CF74078229A");

        /// <summary>PKEY_Device_FriendlyName's format id (property 14).</summary>
        public static readonly Guid DeviceFriendlyNameFormat = new("A45C254E-DF1C-4EFD-8020-67D146A850E0");

        public static readonly Guid Unknown = new("00000000-0000-0000-C000-000000000046");
        public static readonly Guid NotificationClient = new("7991EEC9-7C89-4D85-8390-6C703CEC60C0");
        public static readonly Guid SessionNotification = new("641DD20B-4D41-49CC-ABA3-174B9477BB08");
        public static readonly Guid SessionEvents = new("24918ACC-64B3-37C1-8CA9-74A66E9957A8");
        public static readonly Guid EndpointVolumeCallback = new("657804FA-D6AD-4496-8A60-352752AF4F89");
    }

    private static nint Slot(nint com, int index) => (*(nint**)com)[index];

    /// <summary>A method that takes one interface pointer and returns an HRESULT — every (un)register call here.</summary>
    private static int Call(nint com, int slot, nint arg)
        => ((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(com, slot))(com, arg);

    private static void ReleaseRaw(nint com)
    {
        if (com != 0)
        {
            ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(com, Slots.Release))(com);
        }
    }

    /// <summary>QueryInterface, or 0 when the object does not have it.</summary>
    private static nint Query(nint com, Guid iid)
    {
        nint result;
        return ((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Slot(com, Slots.QueryInterface))(com, &iid, &result) >= 0
            ? result
            : 0;
    }

    /// <summary>A string a COM getter allocated with the task allocator, read and freed.</summary>
    private static string TakeString(nint com, int slot)
    {
        char* text = null;

        try
        {
            return ((delegate* unmanaged[Stdcall]<nint, char**, int>)Slot(com, slot))(com, &text) >= 0 && text is not null
                ? new string(text)
                : string.Empty;
        }
        finally
        {
            if (text is not null)
            {
                CoTaskMemFree(text);
            }
        }
    }

    private static int _lastFailure;

    /// <summary>
    /// Log a failure the first time, and the recovery when it stops; never once per refresh. A
    /// missing device answers identically on every wake for as long as it stays missing.
    /// </summary>
    private static void Note(int result, string? message)
    {
        if (result == _lastFailure)
        {
            return;
        }

        _lastFailure = result;

        if (result >= 0)
        {
            Log.Info(Scope, "the audio devices are answering again");
        }
        else if (message is not null)
        {
            Log.Warn(Scope, $"{message}: 0x{result:X8}");
        }
    }

    // ---- the callback objects --------------------------------------------------------------

    /// <summary>
    /// The four objects Core Audio calls back. Each is a pointer to its vtable in native memory,
    /// allocated once for the life of the process. Reference counting is a no-op on them — they are
    /// immortal — which is what makes a late callback during teardown harmless, and why one object
    /// can be registered with every endpoint and session at once. A callback only sets a flag and
    /// wakes the worker; it never calls back into Core Audio (which the documentation forbids from
    /// inside a notification) and never lets an exception escape.
    /// </summary>
    private static class Callbacks
    {
        public static readonly nint DeviceClient = Build(
        [
            (nint)(delegate* unmanaged<nint, Guid*, nint*, int>)&QueryDeviceClient,
            (nint)(delegate* unmanaged<nint, uint>)&Immortal,
            (nint)(delegate* unmanaged<nint, uint>)&Immortal,
            (nint)(delegate* unmanaged<nint, char*, uint, int>)&OnDeviceStateChanged,
            (nint)(delegate* unmanaged<nint, char*, int>)&OnDeviceAdded,
            (nint)(delegate* unmanaged<nint, char*, int>)&OnDeviceRemoved,
            (nint)(delegate* unmanaged<nint, int, int, char*, int>)&OnDefaultDeviceChanged,
            (nint)(delegate* unmanaged<nint, char*, PropertyKey, int>)&OnPropertyValueChanged,
        ]);

        public static readonly nint SessionCreated = Build(
        [
            (nint)(delegate* unmanaged<nint, Guid*, nint*, int>)&QuerySessionNotification,
            (nint)(delegate* unmanaged<nint, uint>)&Immortal,
            (nint)(delegate* unmanaged<nint, uint>)&Immortal,
            (nint)(delegate* unmanaged<nint, nint, int>)&OnSessionCreated,
        ]);

        public static readonly nint SessionEvents = Build(
        [
            (nint)(delegate* unmanaged<nint, Guid*, nint*, int>)&QuerySessionEvents,
            (nint)(delegate* unmanaged<nint, uint>)&Immortal,
            (nint)(delegate* unmanaged<nint, uint>)&Immortal,
            (nint)(delegate* unmanaged<nint, char*, Guid*, int>)&OnDisplayNameChanged,
            (nint)(delegate* unmanaged<nint, char*, Guid*, int>)&OnIconPathChanged,
            (nint)(delegate* unmanaged<nint, float, int, Guid*, int>)&OnSimpleVolumeChanged,
            (nint)(delegate* unmanaged<nint, uint, float*, uint, Guid*, int>)&OnChannelVolumeChanged,
            (nint)(delegate* unmanaged<nint, Guid*, Guid*, int>)&OnGroupingParamChanged,
            (nint)(delegate* unmanaged<nint, int, int>)&OnStateChanged,
            (nint)(delegate* unmanaged<nint, int, int>)&OnSessionDisconnected,
        ]);

        public static readonly nint EndpointVolume = Build(
        [
            (nint)(delegate* unmanaged<nint, Guid*, nint*, int>)&QueryEndpointVolume,
            (nint)(delegate* unmanaged<nint, uint>)&Immortal,
            (nint)(delegate* unmanaged<nint, uint>)&Immortal,
            (nint)(delegate* unmanaged<nint, nint, int>)&OnNotify,
        ]);

        /// <summary>An object is one pointer, to a vtable. Both live in native memory forever.</summary>
        private static nint Build(nint[] methods)
        {
            var table = (nint*)NativeMemory.Alloc((nuint)(methods.Length * sizeof(nint)));
            for (var i = 0; i < methods.Length; i++)
            {
                table[i] = methods[i];
            }

            var instance = (nint*)NativeMemory.Alloc((nuint)sizeof(nint));
            *instance = (nint)table;
            return (nint)instance;
        }

        private static int Answer(nint self, Guid* iid, nint* result, Guid own)
        {
            if (result is null)
            {
                return unchecked((int)0x80004003); // E_POINTER
            }

            if (*iid == Iids.Unknown || *iid == own)
            {
                *result = self;
                return 0;
            }

            *result = 0;
            return NoInterface;
        }

        [UnmanagedCallersOnly]
        private static int QueryDeviceClient(nint self, Guid* iid, nint* result) => Answer(self, iid, result, Iids.NotificationClient);

        [UnmanagedCallersOnly]
        private static int QuerySessionNotification(nint self, Guid* iid, nint* result) => Answer(self, iid, result, Iids.SessionNotification);

        [UnmanagedCallersOnly]
        private static int QuerySessionEvents(nint self, Guid* iid, nint* result) => Answer(self, iid, result, Iids.SessionEvents);

        [UnmanagedCallersOnly]
        private static int QueryEndpointVolume(nint self, Guid* iid, nint* result) => Answer(self, iid, result, Iids.EndpointVolumeCallback);

        /// <summary>The objects are never freed, so their count never matters.</summary>
        [UnmanagedCallersOnly]
        private static uint Immortal(nint self) => 1;

        /// <summary>
        /// Every notification ends here: set a flag, wake the worker, return. Nothing that can throw
        /// runs outside the try — an exception escaping into Core Audio's thread is a crash.
        /// </summary>
        private static int Wake(Change change)
        {
            try
            {
                Notify(change);
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "a Core Audio notification could not be passed on", ex);
            }

            return 0;
        }

        // [S-1221] Devices coming, going, enabled, disabled: the device list and every binding.
        [UnmanagedCallersOnly]
        private static int OnDeviceStateChanged(nint self, char* id, uint state) => Wake(Change.Devices);

        [UnmanagedCallersOnly]
        private static int OnDeviceAdded(nint self, char* id) => Wake(Change.Devices);

        [UnmanagedCallersOnly]
        private static int OnDeviceRemoved(nint self, char* id) => Wake(Change.Devices);

        [UnmanagedCallersOnly]
        private static int OnDefaultDeviceChanged(nint self, int flow, int role, char* id)
            => role == RoleConsole && flow == DataFlowRender ? Wake(Change.Default) : 0;

        /// <summary>Only a rename matters; the rest of a device's properties change often and mean nothing here.</summary>
        [UnmanagedCallersOnly]
        private static int OnPropertyValueChanged(nint self, char* id, PropertyKey key)
            => key.Format == Iids.DeviceFriendlyNameFormat && key.Id == DeviceFriendlyNameId ? Wake(Change.Devices) : 0;

        [UnmanagedCallersOnly]
        private static int OnSessionCreated(nint self, nint session) => Wake(Change.Sessions);

        [UnmanagedCallersOnly]
        private static int OnDisplayNameChanged(nint self, char* name, Guid* context) => Wake(Change.Sessions);

        [UnmanagedCallersOnly]
        private static int OnIconPathChanged(nint self, char* path, Guid* context) => 0;

        [UnmanagedCallersOnly]
        private static int OnSimpleVolumeChanged(nint self, float level, int muted, Guid* context) => Wake(Change.Sessions);

        [UnmanagedCallersOnly]
        private static int OnChannelVolumeChanged(nint self, uint count, float* levels, uint changed, Guid* context) => 0;

        [UnmanagedCallersOnly]
        private static int OnGroupingParamChanged(nint self, Guid* grouping, Guid* context) => 0;

        [UnmanagedCallersOnly]
        private static int OnStateChanged(nint self, int state) => Wake(Change.Sessions);

        [UnmanagedCallersOnly]
        private static int OnSessionDisconnected(nint self, int reason) => Wake(Change.Sessions);

        /// <summary>[S-1220] A master level or mute moved — this is what replaced the volume tile's poll.</summary>
        [UnmanagedCallersOnly]
        private static int OnNotify(nint self, nint data) => Wake(Change.Sessions);
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoInitializeEx(nint reserved, uint flags);

    [LibraryImport("ole32.dll")]
    private static partial void CoUninitialize();

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(Guid* clsid, nint outer, uint context, Guid* iid, nint* result);

    [LibraryImport("ole32.dll")]
    private static partial void CoTaskMemFree(void* memory);

    [LibraryImport("ole32.dll")]
    private static partial int PropVariantClear(void* value);
}
