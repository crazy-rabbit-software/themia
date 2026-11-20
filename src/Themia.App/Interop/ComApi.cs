using System.Runtime.InteropServices;

namespace Themia.App.Interop;

/// <summary>
/// COM through function-pointer vtables (Native AOT: no built-in COM interop), declared once: the apartment
/// (<see cref="Apartment"/>), object creation (<see cref="Create"/>), a vtable's entry (<see cref="Slot"/>),
/// <see cref="Query"/> and <see cref="Release"/>, and the task allocator's free. A pointer a scope owns is a
/// <see cref="ComPtr"/>, released on every path; a file's own interfaces call their slots through <see cref="Slot"/>.
/// </summary>
internal static unsafe partial class ComApi
{
    public const uint ClsCtxInprocServer = 0x1;
    public const uint ClsCtxLocalServer = 0x4;
    public const uint ClsCtxAll = 0x17;

    /// <summary>COINIT_MULTITHREADED: a pool thread needs no message pump.</summary>
    public const uint MultiThreaded = 0x0;

    /// <summary>COINIT_APARTMENTTHREADED.</summary>
    public const uint ApartmentThreaded = 0x2;

    public const int SlotQueryInterface = 0;
    public const int SlotAddRef = 1;
    public const int SlotRelease = 2;

    /// <summary>RPC_E_CHANGED_MODE: the thread's apartment is already the other kind; it is used as it is.</summary>
    public const int ChangedMode = unchecked((int)0x80010106);

    /// <summary>The vtable entry <paramref name="slot"/> of the COM object <paramref name="self"/>.</summary>
    public static nint Slot(nint self, int slot) => (*(nint**)self)[slot];

    /// <summary>IUnknown::Release, nothing for 0; the count left.</summary>
    public static uint Release(nint self)
        => self == 0 ? 0 : ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(self, SlotRelease))(self);

    /// <summary>IUnknown::AddRef.</summary>
    public static uint AddRef(nint self) => ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(self, SlotAddRef))(self);

    /// <summary>IUnknown::QueryInterface: the HRESULT, <paramref name="result"/> the interface or 0.</summary>
    public static int Query(nint self, Guid iid, out nint result)
    {
        nint found = 0;
        var hr = ((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Slot(self, SlotQueryInterface))(self, &iid, &found);
        result = hr >= 0 ? found : 0;
        return hr;
    }

    /// <summary>QueryInterface, or 0 where the object does not have it.</summary>
    public static nint Query(nint self, Guid iid) => Query(self, iid, out var result) >= 0 ? result : 0;

    /// <summary>CoCreateInstance: the HRESULT, <paramref name="result"/> the object or 0.</summary>
    public static int Create(Guid clsid, Guid iid, uint context, out nint result)
    {
        nint made = 0;
        var hr = CoCreateInstance(&clsid, 0, context, &iid, &made);
        result = hr >= 0 ? made : 0;
        return hr;
    }

    /// <summary>Free what COM's task allocator handed over (a string, an ID list, a format); nothing for 0.</summary>
    public static void Free(void* memory)
    {
        if (memory is not null)
        {
            CoTaskMemFree(memory);
        }
    }

    /// <summary>Free what COM's task allocator handed over; nothing for 0.</summary>
    public static void Free(nint memory) => Free((void*)memory);

    [LibraryImport("ole32.dll")]
    private static partial int CoInitializeEx(nint reserved, uint flags);

    [LibraryImport("ole32.dll")]
    private static partial void CoUninitialize();

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(Guid* clsid, nint outer, uint context, Guid* iid, nint* result);

    [LibraryImport("ole32.dll")]
    private static partial void CoTaskMemFree(void* memory);

    /// <summary>A string the callee allocated with <c>CoTaskMemAlloc</c>, freed once when the scope ends.</summary>
    internal readonly ref struct TaskString
    {
        private readonly char* _text;

        public TaskString(char* text) => _text = text;

        /// <summary>The text, or null for none.</summary>
        public string? Text => _text is null ? null : new string(_text);

        public void Dispose() => Free(_text);
    }

    /// <summary>
    /// COM on this thread for a scope: <c>CoInitializeEx</c> on entry, <c>CoUninitialize</c> on the way out exactly when
    /// the entry succeeded (S_OK or S_FALSE). A thread already in the other kind of apartment (<see cref="ChangedMode"/>)
    /// is used as it is and never uninitialised.
    /// </summary>
    internal readonly ref struct Apartment
    {
        private Apartment(int result) => Result = result;

        /// <summary>CoInitializeEx's HRESULT.</summary>
        public int Result { get; }

        /// <summary>COM can be used on this thread: it entered, or the thread already had another apartment.</summary>
        public bool Usable => Result >= 0 || Result == ChangedMode;

        public static Apartment Enter(uint flags) => new(CoInitializeEx(0, flags));

        public void Dispose()
        {
            if (Result >= 0)
            {
                CoUninitialize();
            }
        }
    }
}

/// <summary>
/// An owned COM interface pointer: released exactly once, on dispose (<c>using</c> in a scope, the owner's Dispose for a
/// field: a drag's data object and its forward target are fields, which is why this is no ref struct). 0 is no object.
/// <see cref="Detach"/> hands the reference on (to OLE, to a caller) without releasing it here; never call it on a
/// <c>using</c> local, which it would only copy.
/// </summary>
internal struct ComPtr : IDisposable
{
    public nint Pointer;

    public ComPtr(nint pointer) => Pointer = pointer;

    public readonly bool IsNull => Pointer == 0;

    /// <summary>Hand the pointer on without releasing it here.</summary>
    public nint Detach()
    {
        var p = Pointer;
        Pointer = 0;
        return p;
    }

    public void Dispose()
    {
        var p = Pointer;
        Pointer = 0;
        _ = ComApi.Release(p);
    }
}
