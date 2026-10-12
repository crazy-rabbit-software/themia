using System.Runtime.InteropServices;
using Themia.App.Services.Toggles;
using Themia.Core.Diagnostics;
using Themia.Core.Shell;

namespace Themia.App.Services.Focus;

/// <summary>
/// [S-4424] Whether Windows' own focus session is on — one started in the Clock app, in Settings or from
/// the notification center — through <c>Windows.UI.Shell.FocusSessionManager</c> (Windows 11): its
/// <c>IsFocusActive</c>, and its <c>IsFocusActiveChanged</c> to hear it change. Nothing polls.
/// <para>
/// <b>Read-only, by Windows' rule.</b> Starting and ending a Windows focus session
/// (<c>TryStartFocusSession</c>, <c>DeactivateFocus</c>) are a Limited Access Feature: Windows answers
/// them only for an app Microsoft has given an unlock token for its package family, and Themia has none
/// (Karpo's <c>DoNotDisturb.cs</c> found the same). Reading the state and hearing it change are open.
/// Windows does not say when its session ends, so this is on or off, never a time.
/// </para>
/// <para>
/// Themia builds against the Windows 10 SDK projection, which has no such class, so it is reached through
/// raw vtables as <c>CoreAudio.Native.cs</c> does: IIDs from the Windows 11 SDK's metadata, and the event
/// handler a small COM object of Themia's own whose IID is the parameterised one Windows asks for
/// (<see cref="WinRtIid"/>). Its <c>Invoke</c> catches everything: nothing may escape into Windows.
/// </para>
/// </summary>
internal sealed unsafe class WindowsFocusWatch : IDisposable
{
    private const string Scope = "focus";
    private const string ClassName = "Windows.UI.Shell.FocusSessionManager";

    /// <summary>IFocusSessionManagerStatics: [6] GetDefault, [7] get_IsSupported.</summary>
    private static readonly Guid StaticsIid = new("834df764-cb9a-5d0a-aa9f-73df4f249395");

    /// <summary>IFocusSessionManager: [6] get_IsFocusActive … [11] add_IsFocusActiveChanged, [12] remove_IsFocusActiveChanged.</summary>
    private static readonly Guid ManagerIid = new("e7ffbaa9-d8be-5dbf-bac6-49364842e37e");

    /// <summary>TypedEventHandler&lt;FocusSessionManager, Object&gt;.</summary>
    private static readonly Guid HandlerIid = WinRtIid.Of(WinRtIid.TypedEventHandlerOfClass(ClassName, ManagerIid));

    private static readonly Guid UnknownIid = new("00000000-0000-0000-c000-000000000046");
    private static readonly Guid AgileIid = new("94ea2b94-e9cc-49e0-c0ff-ee64ca8f5b90");

    private const int SlotGetDefault = 6;
    private const int SlotIsSupported = 7;
    private const int SlotIsFocusActive = 6;
    private const int SlotAddChanged = 11;
    private const int SlotRemoveChanged = 12;

    private readonly Lock _gate = new();
    private readonly Action _changed;
    private nint _manager;
    private Handler* _handler;
    private long _token;
    private bool _subscribed;
    private volatile bool _disposed;

    /// <param name="changed">Raised from any thread when <see cref="Active"/> may have moved.</param>
    public WindowsFocusWatch(Action changed) => _changed = changed;

    /// <summary>On, off, or null where this Windows has no focus sessions to read (or has not answered yet).</summary>
    public bool? Active { get; private set; }

    /// <summary>Begin following, on the pool. Never blocks.</summary>
    public void Start() => _ = Task.Run(() =>
    {
        try
        {
            Open();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "Windows' focus session could not be followed", ex);
        }
    });

    private void Open()
    {
        var statics = Factory();
        if (statics == 0)
        {
            return;
        }

        try
        {
            byte supported = 0;
            var asked = ((delegate* unmanaged[Stdcall]<nint, byte*, int>)ToggleNative.Slot(statics, SlotIsSupported))(statics, &supported);
            if (asked < 0 || supported == 0)
            {
                Log.Info(Scope, $"Windows' focus sessions are not supported here (0x{asked:X8}, {supported})");
                return;
            }

            nint manager = 0;
            var made = ((delegate* unmanaged[Stdcall]<nint, nint*, int>)ToggleNative.Slot(statics, SlotGetDefault))(statics, &manager);
            if (made < 0 || manager == 0)
            {
                Log.Warn(Scope, $"FocusSessionManager.GetDefault answered 0x{made:X8}");
                return;
            }

            var handler = Handler.Create(this);
            long token = 0;
            var added = ((delegate* unmanaged[Stdcall]<nint, Handler*, long*, int>)ToggleNative.Slot(manager, SlotAddChanged))(manager, handler, &token);

            lock (_gate)
            {
                if (_disposed)
                {
                    if (added >= 0)
                    {
                        _ = ((delegate* unmanaged[Stdcall]<nint, long, int>)ToggleNative.Slot(manager, SlotRemoveChanged))(manager, token);
                    }

                    Handler.Release(handler);
                    ToggleNative.Release(manager);
                    return;
                }

                _manager = manager;
                _handler = handler;
                _token = token;
                _subscribed = added >= 0;
            }

            if (added < 0)
            {
                // Read once: without the event a change shows when the widget next asks (Read).
                Log.Warn(Scope, $"IsFocusActiveChanged could not be subscribed (0x{added:X8})");
            }

            Read();
        }
        finally
        {
            ToggleNative.Release(statics);
        }
    }

    /// <summary>Read the state again (the event's handler, a widget's pointer). Any thread.</summary>
    public void Read()
    {
        nint manager;
        lock (_gate)
        {
            manager = _manager;
            if (manager == 0 || _disposed)
            {
                return;
            }

            _ = ((delegate* unmanaged[Stdcall]<nint, uint>)ToggleNative.Slot(manager, 1))(manager);
        }

        try
        {
            byte active = 0;
            var result = ((delegate* unmanaged[Stdcall]<nint, byte*, int>)ToggleNative.Slot(manager, SlotIsFocusActive))(manager, &active);
            bool? now = result < 0 ? null : active != 0;
            if (result < 0)
            {
                Log.Warn(Scope, $"IsFocusActive answered 0x{result:X8}");
            }

            if (now != Active)
            {
                Active = now;
                _changed();
            }
        }
        finally
        {
            ToggleNative.Release(manager);
        }
    }

    private static nint Factory()
    {
        var name = ToggleNative.HString(ClassName);
        if (name == 0)
        {
            return 0;
        }

        try
        {
            nint factory = 0;
            var iid = StaticsIid;
            var result = ToggleNative.RoGetActivationFactory(name, &iid, &factory);
            if (result < 0 || factory == 0)
            {
                // Windows 10, or a Windows 11 before focus sessions had an API.
                Log.Info(Scope, $"no FocusSessionManager here (0x{result:X8})");
                return 0;
            }

            return factory;
        }
        finally
        {
            _ = ToggleNative.WindowsDeleteString(name);
        }
    }

    public void Dispose()
    {
        nint manager;
        Handler* handler;
        long token;
        bool subscribed;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            manager = _manager;
            handler = _handler;
            token = _token;
            subscribed = _subscribed;
            _manager = 0;
            _handler = null;
        }

        if (manager != 0 && subscribed)
        {
            var removed = ((delegate* unmanaged[Stdcall]<nint, long, int>)ToggleNative.Slot(manager, SlotRemoveChanged))(manager, token);
            if (removed < 0)
            {
                Log.Warn(Scope, $"IsFocusActiveChanged could not be unsubscribed (0x{removed:X8})");
            }
        }

        if (handler is not null)
        {
            Handler.Release(handler);
        }

        ToggleNative.Release(manager);
    }

    /// <summary>
    /// The event handler Windows calls: a COM object of three IUnknown slots and Invoke, counted, agile,
    /// pointing back at its watch through a weak GC handle so a forgotten watch is never kept alive.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Handler
    {
        private static readonly nint* Table = MakeTable();

        public nint* Vtable;
        public int References;
        public nint Watch;

        public static Handler* Create(WindowsFocusWatch watch)
        {
            var handler = (Handler*)NativeMemory.Alloc((nuint)sizeof(Handler));
            handler->Vtable = Table;
            handler->References = 1;
            handler->Watch = GCHandle.ToIntPtr(GCHandle.Alloc(watch, GCHandleType.Weak));
            return handler;
        }

        public static void Release(Handler* handler) => _ = Drop(handler);

        private static nint* MakeTable()
        {
            var table = (nint*)NativeMemory.Alloc((nuint)(sizeof(nint) * 4));
            table[0] = (nint)(delegate* unmanaged[Stdcall]<Handler*, Guid*, nint*, int>)&QueryInterface;
            table[1] = (nint)(delegate* unmanaged[Stdcall]<Handler*, uint>)&AddRef;
            table[2] = (nint)(delegate* unmanaged[Stdcall]<Handler*, uint>)&ReleaseRef;
            table[3] = (nint)(delegate* unmanaged[Stdcall]<Handler*, nint, nint, int>)&Invoke;
            return table;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
        private static int QueryInterface(Handler* self, Guid* iid, nint* result)
        {
            if (result is null)
            {
                return unchecked((int)0x80004003); // E_POINTER
            }

            if (*iid == UnknownIid || *iid == AgileIid || *iid == HandlerIid)
            {
                _ = Interlocked.Increment(ref self->References);
                *result = (nint)self;
                return 0;
            }

            *result = 0;
            return unchecked((int)0x80004002); // E_NOINTERFACE
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
        private static uint AddRef(Handler* self) => (uint)Interlocked.Increment(ref self->References);

        [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
        private static uint ReleaseRef(Handler* self) => Drop(self);

        private static uint Drop(Handler* self)
        {
            var left = Interlocked.Decrement(ref self->References);
            if (left == 0)
            {
                GCHandle.FromIntPtr(self->Watch).Free();
                NativeMemory.Free(self);
            }

            return (uint)Math.Max(left, 0);
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
        private static int Invoke(Handler* self, nint sender, nint args)
        {
            try
            {
                if (GCHandle.FromIntPtr(self->Watch).Target is WindowsFocusWatch watch && !watch._disposed)
                {
                    // Off Windows' thread: reading back inside its callback could wait on it.
                    _ = Task.Run(watch.Read);
                }
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "a focus session change could not be read", ex);
            }

            return 0;
        }
    }
}
