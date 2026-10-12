using Microsoft.Win32;
using Themia.Core.Diagnostics;

namespace Themia.App.Services.Toggles;

/// <summary>
/// [S-3907] A registry key followed for changes, for what Windows keeps there and announces nowhere
/// else an app can hear: the radio management service's state, a setting's DWORD, the rotation key.
/// <c>RegNotifyChangeKeyValue</c> signals an event, the pool waits on it, and the watch is armed
/// again before the change is passed on, so a change made meanwhile is not lost. Thread-agnostic: the
/// pool thread that armed it may be gone by the time the key changes.
/// <para>
/// A key that is not there yet (color filters' key, before they were first used) is waited for at
/// its parent: when something under the parent changes and the key has appeared, the key itself is
/// watched from then on, and the owner told. Nothing here reads a value: the owner reads what it needs.
/// </para>
/// </summary>
internal sealed class RegistryWatch : IDisposable
{
    private const string Scope = "toggles";

    private readonly RegistryHive _hive;
    private readonly string _path;
    private readonly Action _changed;
    private readonly Lock _gate = new();
    private Armed? _armed;
    private bool _onParent;
    private bool _disposed;

    private RegistryWatch(RegistryHive hive, string path, Action changed)
    {
        _hive = hive;
        _path = path;
        _changed = changed;
    }

    /// <summary>
    /// Follow <paramref name="path"/> under <paramref name="hive"/> (64-bit view); <paramref name="changed"/>
    /// comes on a pool thread after each change. Null where neither the key nor its parent can be watched.
    /// </summary>
    public static RegistryWatch? Open(RegistryHive hive, string path, Action changed)
    {
        var watch = new RegistryWatch(hive, path, changed);
        if (watch.ArmKey() || watch.ArmParent())
        {
            return watch;
        }

        Log.Warn(Scope, $"cannot follow {hive}\\{path}: changes made elsewhere will show on the next look");
        watch.Dispose();
        return null;
    }

    private bool ArmKey()
    {
        var key = OpenKey(_path);
        if (key is null)
        {
            return false;
        }

        return Arm(key, parent: false);
    }

    private bool ArmParent()
    {
        var cut = _path.LastIndexOf('\\');
        var key = cut > 0 ? OpenKey(_path[..cut]) : null;
        return key is not null && Arm(key, parent: true);
    }

    private RegistryKey? OpenKey(string path)
    {
        try
        {
            using var root = RegistryKey.OpenBaseKey(_hive, RegistryView.Registry64);
            return root.OpenSubKey(path, writable: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log.Warn(Scope, $"cannot open {_hive}\\{path}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Take <paramref name="key"/> and ask for its next change; false (and the key closed) where Windows refused.</summary>
    private bool Arm(RegistryKey key, bool parent)
    {
        var armed = new Armed(key);
        lock (_gate)
        {
            if (_disposed)
            {
                armed.Dispose();
                return false;
            }

            if (!armed.Ask(this))
            {
                armed.Dispose();
                return false;
            }

            _armed?.Dispose();
            _armed = armed;
            _onParent = parent;
            return true;
        }
    }

    /// <summary>On a pool thread: re-armed first, then told. Nothing may escape into the pool's wait.</summary>
    private void Heard(Armed which)
    {
        try
        {
            bool parent;
            lock (_gate)
            {
                if (_disposed || !ReferenceEquals(which, _armed))
                {
                    return;
                }

                parent = _onParent;
            }

            if (parent)
            {
                // Something under the parent changed: once the key is there, it is watched instead.
                if (ArmKey())
                {
                    _changed();
                    return;
                }

                lock (_gate)
                {
                    if (!_disposed && ReferenceEquals(which, _armed) && !which.Ask(this))
                    {
                        Log.Warn(Scope, $"stopped waiting for {_hive}\\{_path} to appear");
                    }
                }

                return;
            }

            bool again;
            lock (_gate)
            {
                if (_disposed || !ReferenceEquals(which, _armed))
                {
                    return;
                }

                again = which.Ask(this);
            }

            if (!again)
            {
                // The key went (deleted with its tree): wait for it at the parent again.
                _ = ArmParent();
            }

            _changed();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"a change under {_hive}\\{_path} could not be passed on", ex);
        }
    }

    public void Dispose()
    {
        Armed? armed;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            armed = _armed;
            _armed = null;
        }

        armed?.Dispose();
    }

    /// <summary>One key, its event and its registered wait: freed together, on every path.</summary>
    private sealed class Armed(RegistryKey key) : IDisposable
    {
        private readonly AutoResetEvent _signal = new(false);
        private RegisteredWaitHandle? _wait;

        /// <summary>Asks for the next change of the key; false where Windows refused.</summary>
        public bool Ask(RegistryWatch owner)
        {
            _wait?.Unregister(null);
            _wait = null;

            var filter = ToggleNative.RegNotifyName | ToggleNative.RegNotifyLastSet | ToggleNative.RegNotifyThreadAgnostic;
            if (ToggleNative.RegNotifyChangeKeyValue(key.Handle, subtree: false, filter, _signal.SafeWaitHandle, asynchronous: true) != 0)
            {
                return false;
            }

            _wait = ThreadPool.RegisterWaitForSingleObject(
                _signal,
                static (state, _) =>
                {
                    var (watch, armed) = ((RegistryWatch, Armed))state!;
                    watch.Heard(armed);
                },
                (owner, this),
                Timeout.Infinite,
                executeOnlyOnce: true);
            return true;
        }

        public void Dispose()
        {
            _wait?.Unregister(null);
            _wait = null;

            // Closing the key ends the notification (it signals the event, which nothing waits on any more).
            key.Dispose();
            _signal.Dispose();
        }
    }
}
