using System.Collections.Concurrent;
using Themia.Core.Diagnostics;

namespace Themia.App.Services.Toggles;

/// <summary>
/// [S-3908] One of Windows' own quick-settings tiles, through its Settings handler: the one road for the
/// switches Windows lets no app flip through a public API (energy saver, night light, game mode, color
/// filters, mono audio). Ported from Karpo (<c>SettingHandlers.cs</c>), where each was read and switched
/// on Windows 11 26200/26300.
/// <para>
/// Each area of Settings has a DLL in System32 exporting <c>GetSetting(HSTRING id, ISettingItem**)</c>;
/// the tile reads and sets the item's "Value". <c>ISettingItem</c> is undocumented (IID
/// {40C037CC-D8BF-489E-8697-D66BAA3221BF}, an IInspectable; slots 6 get_Id, 7 get_Type, 9 get_IsEnabled,
/// 10 get_IsApplicable, 13 GetValue, 14 SetValue). Every item is checked before it is used: it must
/// say back the id it was asked for, be a switch (type 3) and hold a boolean, or it is not used and the
/// widget says Windows does not let apps switch it here.
/// </para>
/// <para>
/// <b>The handlers live on one thread of their own</b>: a single-threaded apartment waiting on its work
/// and its messages together (<c>MsgWaitForMultipleObjectsEx</c>), because a handler finishes starting
/// up on that loop. Every call is handed to it as a task; nothing waits on a wall clock. The libraries
/// are never unloaded, since a handler's own threads may outlive the item.
/// </para>
/// </summary>
internal sealed unsafe class SettingHandler
{
    private const string Scope = "toggles";

    private static readonly Guid ItemInterface = new("40C037CC-D8BF-489E-8697-D66BAA3221BF");
    private static readonly Guid PropertyValueInterface = new("4BD682DD-7554-40E9-9A9B-82654EDE7E62");
    private static readonly Guid PropertyValueStatics = new("629BDBC8-D932-4FF4-96B9-8D96C5C1E858");

    private const int SwitchType = 3;
    private const int BooleanType = 11;

    private const int SlotId = 6;
    private const int SlotType = 7;
    private const int SlotEnabled = 9;
    private const int SlotApplicable = 10;
    private const int SlotGetValue = 13;
    private const int SlotSetValue = 14;
    private const int SlotValueType = 6;
    private const int SlotGetBoolean = 18;
    private const int SlotCreateBoolean = 17;

    private readonly string _id;

    // Owned by the handlers' thread and only touched there.
    private nint _item;

    private SettingHandler(nint item, string id)
    {
        _item = item;
        _id = id;
    }

    /// <summary>The handler for a tile, or null where it is not there or not the switch expected.</summary>
    public static Task<SettingHandler?> OpenAsync(string library, string id)
        => HandlerThread.Run(() => Make(library, id), null);

    /// <summary>Whether Windows offers it here: the handler says it applies and may be changed.</summary>
    public Task<bool> UsableAsync() => HandlerThread.Run(() => _item != 0 && Flag(_item, SlotApplicable) && Flag(_item, SlotEnabled), false);

    /// <summary>Where it stands, or null where the handler did not say.</summary>
    public Task<bool?> ValueAsync() => HandlerThread.Run(() => _item == 0 ? null : Read(_item), (bool?)null);

    /// <summary>Switches it; false where the handler refused.</summary>
    public Task<bool> SetAsync(bool on) => HandlerThread.Run(() => _item != 0 && Write(_item, on), false);

    /// <summary>The item goes on the thread it belongs to.</summary>
    public void Release() => HandlerThread.Post(() =>
    {
        ToggleNative.Release(_item);
        _item = 0;
    });

    private static SettingHandler? Make(string library, string id)
    {
        var module = ToggleNative.LoadLibraryEx(library, 0, ToggleNative.LoadFromSystem32);
        if (module == 0)
        {
            Log.Info(Scope, $"{library} is not on this PC");
            return null;
        }

        nint export;
        fixed (byte* name = "GetSetting"u8)
        {
            export = ToggleNative.GetProcAddress(module, name);
        }

        if (export == 0)
        {
            Log.Warn(Scope, $"{library} has no GetSetting");
            return null;
        }

        nint found = 0;
        var text = ToggleNative.HString(id);
        int asked;
        try
        {
            asked = ((delegate* unmanaged[Stdcall]<nint, nint*, int>)export)(text, &found);
        }
        finally
        {
            _ = ToggleNative.WindowsDeleteString(text);
        }

        if (asked < 0 || found == 0)
        {
            Log.Warn(Scope, $"{id}: GetSetting answered 0x{asked:X8}");
            return null;
        }

        var item = ToggleNative.Query(found, ItemInterface);
        ToggleNative.Release(found);
        if (item == 0)
        {
            Log.Warn(Scope, $"{id} is not an ISettingItem");
            return null;
        }

        if (Id(item) != id || Type(item) != SwitchType || Read(item) is null)
        {
            Log.Warn(Scope, $"{id}: the handler is not the switch it was asked for; not used");
            ToggleNative.Release(item);
            return null;
        }

        return new SettingHandler(item, id);
    }

    private static string? Id(nint item)
    {
        nint text = 0;
        if (((delegate* unmanaged[Stdcall]<nint, nint*, int>)ToggleNative.Slot(item, SlotId))(item, &text) < 0)
        {
            return null;
        }

        try
        {
            return ToggleNative.HStringText(text);
        }
        finally
        {
            _ = ToggleNative.WindowsDeleteString(text);
        }
    }

    private static int Type(nint item)
    {
        var type = -1;
        return ((delegate* unmanaged[Stdcall]<nint, int*, int>)ToggleNative.Slot(item, SlotType))(item, &type) < 0 ? -1 : type;
    }

    private static bool Flag(nint item, int slot)
    {
        byte flag = 0;
        return ((delegate* unmanaged[Stdcall]<nint, byte*, int>)ToggleNative.Slot(item, slot))(item, &flag) >= 0 && flag != 0;
    }

    /// <summary>The item's "Value" as a boolean, or null where it is not one.</summary>
    private static bool? Read(nint item)
    {
        nint value = 0;
        var name = ToggleNative.HString("Value");
        int asked;
        try
        {
            asked = ((delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)ToggleNative.Slot(item, SlotGetValue))(item, name, &value);
        }
        finally
        {
            _ = ToggleNative.WindowsDeleteString(name);
        }

        if (asked < 0 || value == 0)
        {
            return null;
        }

        var property = ToggleNative.Query(value, PropertyValueInterface);
        try
        {
            if (property == 0)
            {
                return null;
            }

            var type = -1;
            byte flag = 0;
            if (((delegate* unmanaged[Stdcall]<nint, int*, int>)ToggleNative.Slot(property, SlotValueType))(property, &type) < 0 || type != BooleanType
                || ((delegate* unmanaged[Stdcall]<nint, byte*, int>)ToggleNative.Slot(property, SlotGetBoolean))(property, &flag) < 0)
            {
                return null;
            }

            return flag != 0;
        }
        finally
        {
            ToggleNative.Release(property);
            ToggleNative.Release(value);
        }
    }

    /// <summary>Sets the item's "Value" to <paramref name="on"/>, boxed as Windows boxes a boolean.</summary>
    private static bool Write(nint item, bool on)
    {
        nint statics = 0;
        var wanted = PropertyValueStatics;
        var factory = ToggleNative.HString("Windows.Foundation.PropertyValue");
        int made;
        try
        {
            made = ToggleNative.RoGetActivationFactory(factory, &wanted, &statics);
        }
        finally
        {
            _ = ToggleNative.WindowsDeleteString(factory);
        }

        if (made < 0 || statics == 0)
        {
            Log.Warn(Scope, $"PropertyValue's factory answered 0x{made:X8}");
            return false;
        }

        nint boxed = 0;
        var name = ToggleNative.HString("Value");
        try
        {
            if (((delegate* unmanaged[Stdcall]<nint, byte, nint*, int>)ToggleNative.Slot(statics, SlotCreateBoolean))(statics, on ? (byte)1 : (byte)0, &boxed) < 0 || boxed == 0)
            {
                return false;
            }

            var set = ((delegate* unmanaged[Stdcall]<nint, nint, nint, int>)ToggleNative.Slot(item, SlotSetValue))(item, name, boxed);
            if (set < 0)
            {
                Log.Warn(Scope, $"setting a tile's value answered 0x{set:X8}");
            }

            return set >= 0;
        }
        finally
        {
            _ = ToggleNative.WindowsDeleteString(name);
            ToggleNative.Release(boxed);
            ToggleNative.Release(statics);
        }
    }
}

/// <summary>
/// [S-3908] The one thread the Settings handlers live on: a single-threaded apartment waiting on its
/// work and its messages together, started by the first handler and kept for the life of the app.
/// </summary>
internal static unsafe class HandlerThread
{
    private const string Scope = "toggles";

    private static readonly ConcurrentQueue<Action> Work = new();
    private static readonly AutoResetEvent Wake = new(false);
    private static readonly Lock Gate = new();
    private static Thread? _thread;

    /// <summary>Run <paramref name="work"/> there; its answer, or <paramref name="otherwise"/> where it threw.</summary>
    public static Task<T> Run<T>(Func<T> work, T otherwise)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(() =>
        {
            try
            {
                done.SetResult(work());
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "a Settings handler call failed", ex);
                done.SetResult(otherwise);
            }
        });
        return done.Task;
    }

    /// <summary>Hand <paramref name="work"/> to the thread without waiting for it.</summary>
    public static void Post(Action work)
    {
        Ensure();
        Work.Enqueue(work);
        Wake.Set();
    }

    private static void Ensure()
    {
        lock (Gate)
        {
            if (_thread is not null)
            {
                return;
            }

            _thread = new Thread(Loop) { IsBackground = true, Name = "Themia settings handlers" };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }
    }

    private static void Loop()
    {
        try
        {
            var wake = Wake.SafeWaitHandle.DangerousGetHandle();
            while (true)
            {
                _ = ToggleNative.MsgWaitForMultipleObjectsEx(1, &wake, ToggleNative.Infinite, ToggleNative.QsAllInput, ToggleNative.MwmoInputAvailable);
                while (Work.TryDequeue(out var job))
                {
                    // A job that throws is logged inside Run; Post's own jobs are guarded here.
                    try
                    {
                        job();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(Scope, "a Settings handler job failed", ex);
                    }
                }

                Pump();
            }
        }
        catch (Exception ex)
        {
            // A thread's exception would end the process; the toggles behind it stay unanswered instead.
            Log.Error(Scope, "the Settings handlers' thread stopped", ex);
        }
    }

    /// <summary>Hand on whatever messages are waiting for this thread's windows.</summary>
    private static void Pump()
    {
        ToggleNative.Msg message;
        while (ToggleNative.PeekMessage(&message, 0, 0, 0, ToggleNative.PmRemove))
        {
            _ = ToggleNative.TranslateMessage(&message);
            _ = ToggleNative.DispatchMessage(&message);
        }
    }
}
