using System.Runtime.InteropServices;
using Themia.App.Interop;
using Themia.Core.Diagnostics;

namespace Themia.App.Services;

/// <summary>
/// Windows telling this app that the clipboard changed, and this app putting something back on it.
/// <para>
/// A message-only window with <c>AddClipboardFormatListener</c>, never a poll. A poll would read
/// the clipboard on a timer forever — which is both a real cost and, on the most sensitive surface
/// in the system, the wrong posture: this reads the clipboard exactly when Windows says it changed,
/// and never otherwise.
/// </para>
/// <para>
/// <b>Text only.</b> Only <c>CF_UNICODETEXT</c> is ever asked for. A copied file, image or private
/// format is not read, not cached and not counted — the widget simply does not notice it.
/// </para>
/// <para>
/// <b>Every handle is released on every path.</b> The clipboard is a process-wide lock: a handler
/// that opens it and returns without closing it wedges copy and paste for every application on the
/// desktop until this process exits. So every open is paired with a <c>finally</c>, every
/// <c>GlobalLock</c> with an unlock, and the one allocation that can be orphaned — the buffer whose
/// ownership only transfers to the clipboard on a <i>successful</i> <c>SetClipboardData</c> — is
/// freed on both failure paths.
/// </para>
/// <para>
/// One listener serves every Clipboard widget on every display: subscribers are counted, the window
/// is created with the first and destroyed with the last. It lives on the UI thread, whose message
/// pump is already running, so there is no second thread and no marshalling — and
/// <see cref="Subscribe"/> must therefore be called from it.
/// </para>
/// </summary>
internal static unsafe partial class ClipboardWatcher
{
    private const string Scope = "clipboard";
    private const string ClassName = "ThemiaClipboardWatcher";

    private const uint WmClipboardUpdate = 0x031D;
    private const uint WmDestroy = 0x0002;
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;

    /// <summary>
    /// The longest clipboard string read. Past this the entry would be refused by the history
    /// anyway, so the copy out of the shared buffer is never made in the first place.
    /// </summary>
    private const int MaxRead = 128 * 1024;

    private static readonly List<Action<string>> Listeners = [];

    private static ushort _atom;
    private static nint _hwnd;
    private static bool _listening;

    /// <summary>Whether a listener is actually running. False means the widget has nothing to show.</summary>
    public static bool IsRunning => _listening;

    /// <summary>
    /// Start hearing about clipboard changes. Dispose the result to stop; the listener is torn down
    /// when the last subscriber goes.
    /// <para>
    /// The current clipboard text is delivered to the new subscriber immediately, so a widget
    /// placed on the desktop shows something rather than waiting for the next copy.
    /// </para>
    /// </summary>
    public static IDisposable Subscribe(Action<string> onText)
    {
        ArgumentNullException.ThrowIfNull(onText);

        Listeners.Add(onText);

        if (Listeners.Count == 1)
        {
            Start();
        }

        var current = ReadText();

        if (current is { Length: > 0 })
        {
            Deliver(onText, current);
        }

        return new Subscription(onText);
    }

    /// <summary>
    /// Put text back on the clipboard, which is what clicking a history row does. Returns whether
    /// it landed; never throws.
    /// </summary>
    public static bool SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
        {
            return false;
        }

        if (!OpenClipboard(_hwnd))
        {
            // Another application is holding the clipboard open. Nothing happened, and saying so is
            // better than retrying on a timer until it does.
            Log.Warn(Scope, $"could not open the clipboard to write (win32 {Marshal.GetLastWin32Error()})");
            return false;
        }

        try
        {
            if (!EmptyClipboard())
            {
                Log.Warn(Scope, $"EmptyClipboard failed (win32 {Marshal.GetLastWin32Error()})");
                return false;
            }

            var bytes = (nuint)((text.Length + 1) * sizeof(char));
            var buffer = GlobalAlloc(GmemMoveable, bytes);

            if (buffer == 0)
            {
                Log.Warn(Scope, $"GlobalAlloc failed (win32 {Marshal.GetLastWin32Error()})");
                return false;
            }

            // From here the buffer is owned by this function until SetClipboardData succeeds, and
            // has to be freed on every path that does not get there.
            var destination = GlobalLock(buffer);

            if (destination == 0)
            {
                Log.Warn(Scope, $"GlobalLock failed (win32 {Marshal.GetLastWin32Error()})");

                // ignore: GlobalFree returns the handle on failure, and there is nothing further to
                // do about a buffer that cannot be released.
                GlobalFree(buffer);
                return false;
            }

            fixed (char* source = text)
            {
                Buffer.MemoryCopy(source, (void*)destination, (long)bytes, (long)text.Length * sizeof(char));
            }

            ((char*)destination)[text.Length] = '\0';

            // ignore: GlobalUnlock returns false once the lock count reaches zero, which is the
            // expected outcome here, so its return value says nothing.
            GlobalUnlock(buffer);

            if (SetClipboardData(CfUnicodeText, buffer) == 0)
            {
                // Ownership only transfers on success, so this one is still ours.
                Log.Warn(Scope, $"SetClipboardData failed (win32 {Marshal.GetLastWin32Error()})");

                // ignore: as above — the free is the recovery, its result changes nothing.
                GlobalFree(buffer);
                return false;
            }

            return true;
        }
        finally
        {
            // ignore: the close is the release itself. A failure here means the clipboard was
            // already closed, which is the state this path wants either way.
            CloseClipboard();
        }
    }

    // ---- the listener --------------------------------------------------------------------------

    private static void Start()
    {
        if (_listening)
        {
            return;
        }

        if (!CreateHost())
        {
            return;
        }

        if (!AddClipboardFormatListener(_hwnd))
        {
            Log.Error(Scope, $"AddClipboardFormatListener failed (win32 {Marshal.GetLastWin32Error()})");
            DestroyHost();
            return;
        }

        _listening = true;
    }

    private static bool CreateHost()
    {
        var instance = Win32.GetModuleHandle(null);

        if (_atom == 0)
        {
            fixed (char* className = ClassName)
            {
                var wc = new Win32.WndClassEx
                {
                    Size = (uint)sizeof(Win32.WndClassEx),
                    WndProc = (nint)(delegate* unmanaged<nint, uint, nint, nint, nint>)&WndProc,
                    Instance = instance,
                    ClassName = (nint)className,
                };

                _atom = Win32.RegisterClassEx(ref wc);
            }

            if (_atom == 0)
            {
                Log.Error(Scope, $"RegisterClassEx failed (win32 {Marshal.GetLastWin32Error()})");
                return false;
            }
        }

        // HWND_MESSAGE: a window that exists only to receive messages. It is never shown, never in
        // the z-order and never in Alt+Tab, which is exactly what a clipboard listener wants.
        _hwnd = Win32.CreateWindowEx(0, ClassName, null, 0, 0, 0, 0, 0, Win32.HwndMessage, 0, instance, 0);

        if (_hwnd == 0)
        {
            Log.Error(Scope, $"CreateWindowEx failed (win32 {Marshal.GetLastWin32Error()})");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Take the listener down. Both halves, in order, and both safe to reach twice: the listener is
    /// removed before the window it is registered against is destroyed.
    /// </summary>
    private static void Stop()
    {
        if (_hwnd != 0 && _listening && !RemoveClipboardFormatListener(_hwnd))
        {
            Log.Warn(Scope, $"RemoveClipboardFormatListener failed (win32 {Marshal.GetLastWin32Error()})");
        }

        _listening = false;
        DestroyHost();
    }

    private static void DestroyHost()
    {
        if (_hwnd == 0)
        {
            return;
        }

        if (!Win32.DestroyWindow(_hwnd))
        {
            Log.Warn(Scope, $"DestroyWindow failed (win32 {Marshal.GetLastWin32Error()})");
        }

        _hwnd = 0;
    }

    [UnmanagedCallersOnly]
    private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        // A WndProc is called by Windows itself: a managed exception unwinding out of here is
        // undefined behaviour, so every path is wrapped and falls through to DefWindowProc.
        try
        {
            switch (msg)
            {
                case WmClipboardUpdate:
                    OnClipboardChanged();
                    return 0;

                case WmDestroy:
                    // The window is going away under us — the session ending, say. Forget it rather
                    // than leaving a stale handle that a later Stop would destroy twice.
                    if (hwnd == _hwnd)
                    {
                        _hwnd = 0;
                        _listening = false;
                    }

                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"the clipboard window procedure threw handling message 0x{msg:X}", ex);
        }

        return Win32.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private static void OnClipboardChanged()
    {
        var text = ReadText();

        if (text is not { Length: > 0 })
        {
            return;
        }

        // A snapshot, because a subscriber is free to unsubscribe from inside its own callback —
        // a widget torn down by the change it was just told about.
        foreach (var listener in Listeners.ToArray())
        {
            Deliver(listener, text);
        }
    }

    private static void Deliver(Action<string> listener, string text)
    {
        try
        {
            listener(text);
        }
        catch (Exception ex)
        {
            // One widget failing must not stop the others hearing about the change, and must
            // certainly not unwind into the WndProc.
            Log.Error(Scope, "a clipboard subscriber threw", ex);
        }
    }

    /// <summary>
    /// The clipboard's Unicode text, or null when there is none — which includes every case where
    /// what was copied is a file, an image or a format this app does not read.
    /// </summary>
    private static string? ReadText()
    {
        if (!IsClipboardFormatAvailable(CfUnicodeText))
        {
            // Not an error: somebody copied a file or a picture. Asked before opening, so the
            // clipboard is not locked for the common case of a copy this widget ignores.
            return null;
        }

        if (!OpenClipboard(_hwnd))
        {
            Log.Debug(Scope, $"could not open the clipboard to read (win32 {Marshal.GetLastWin32Error()})");
            return null;
        }

        try
        {
            // Not ours and never freed: the clipboard owns this handle for as long as it is open.
            var handle = GetClipboardData(CfUnicodeText);

            if (handle == 0)
            {
                return null;
            }

            var pointer = GlobalLock(handle);

            if (pointer == 0)
            {
                Log.Debug(Scope, $"GlobalLock on the clipboard buffer failed (win32 {Marshal.GetLastWin32Error()})");
                return null;
            }

            try
            {
                // Bounded by the buffer's own size, not by hunting for a terminator: a buffer that
                // is not NUL-terminated would otherwise be read off the end of the allocation.
                var characters = (int)Math.Min((ulong)GlobalSize(handle) / (ulong)sizeof(char), (ulong)MaxRead);

                if (characters <= 0)
                {
                    return null;
                }

                var span = new ReadOnlySpan<char>((void*)pointer, characters);
                var end = span.IndexOf('\0');

                return end >= 0 ? new string(span[..end]) : new string(span);
            }
            finally
            {
                // ignore: false here means the lock count reached zero, which is what was asked for.
                GlobalUnlock(handle);
            }
        }
        finally
        {
            // ignore: see the write path — a failed close means it was not open.
            CloseClipboard();
        }
    }

    /// <summary>One widget's interest in the clipboard. Disposing the last one takes the listener down.</summary>
    private sealed class Subscription(Action<string> listener) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Listeners.Remove(listener);

            if (Listeners.Count == 0)
            {
                Stop();
            }
        }
    }

    // ---- interop -------------------------------------------------------------------------------
    //
    // Declared here rather than in Win32.cs because nothing else in the app touches the clipboard,
    // and a handle-owning API is easier to review beside the code that pairs its acquire and
    // release. LibraryImport, so the marshalling stubs are source generated: DllImport builds them
    // by emitting IL at runtime, which does not exist under Native AOT.

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AddClipboardFormatListener(nint hwnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RemoveClipboardFormatListener(nint hwnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenClipboard(nint hWndNewOwner);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseClipboard();

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyClipboard();

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsClipboardFormatAvailable(uint format);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint GetClipboardData(uint format);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint SetClipboardData(uint format, nint hMem);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint GlobalAlloc(uint flags, nuint bytes);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint GlobalLock(nint hMem);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(nint hMem);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint GlobalFree(nint hMem);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nuint GlobalSize(nint hMem);
}
