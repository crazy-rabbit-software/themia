using Microsoft.Win32;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services.Toggles;

/// <summary>
/// [S-3907] A registry key followed for changes, for what Windows keeps there and announces nowhere
/// else an app can hear: the radio management service's state, a setting's DWORD, the rotation key, a
/// CloudStore value. <c>RegNotifyChangeKeyValue</c> signals an event, the pool waits on it, and the
/// watch is armed again before the change is passed on, so a change made meanwhile is not lost.
/// Thread-agnostic: the pool thread that armed it may be gone by the time the key changes.
/// <para>
/// A key that is not there (color filters' key before they were first used, a CloudStore key written
/// again from scratch) is waited for at its parent; the steps, and the order that keeps a key coming
/// back in between from being lost, are <see cref="KeyFollow"/> ([S-3921]). Signals are taken one at a
/// time. Nothing here reads a value: the owner reads what it needs.
/// </para>
/// </summary>
internal sealed class RegistryWatch : IDisposable, IKeyArms
{
    private const string Scope = "toggles";

    private readonly RegistryHive _hive;
    private readonly string _path;
    private readonly bool _subtree;
    private readonly Action _changed;
    private readonly Lock _gate = new();

    // Held for one signal's steps, so two signals of one watch never re-arm it at once.
    private readonly Lock _steps = new();
    private Armed? _armed;
    private KeyWatched _at = KeyWatched.Nothing;
    private bool _disposed;

    private RegistryWatch(RegistryHive hive, string path, bool subtree, Action changed)
    {
        _hive = hive;
        _path = path;
        _subtree = subtree;
        _changed = changed;
    }

    /// <summary>
    /// Follow <paramref name="path"/> under <paramref name="hive"/> (64-bit view), with every key under it
    /// where <paramref name="subtree"/>; <paramref name="changed"/> comes on a pool thread after each
    /// change. Null where neither the key nor its parent can be watched.
    /// </summary>
    public static RegistryWatch? Open(RegistryHive hive, string path, Action changed, bool subtree = false)
    {
        var watch = new RegistryWatch(hive, path, subtree, changed);
        lock (watch._steps)
        {
            watch._at = KeyFollow.Open(watch);
        }

        if (watch._at != KeyWatched.Nothing)
        {
            return watch;
        }

        Log.Warn(Scope, $"cannot follow {hive}\\{path}: changes made elsewhere will show on the next look");
        watch.Dispose();
        return null;
    }

    bool IKeyArms.ArmKey() => OpenKey(_path) is { } key && Arm(key, _subtree);

    bool IKeyArms.ArmParent()
    {
        var cut = _path.LastIndexOf('\\');
        return cut > 0 && OpenKey(_path[..cut]) is { } key && Arm(key, subtree: false);
    }

    bool IKeyArms.AskAgain()
    {
        lock (_gate)
        {
            return !_disposed && _armed is { } armed && armed.Ask(this);
        }
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

    /// <summary>Take <paramref name="key"/> in place of what was armed and ask for its next change; false (and the key closed) where Windows refused.</summary>
    private bool Arm(RegistryKey key, bool subtree)
    {
        var armed = new Armed(key, subtree);
        lock (_gate)
        {
            if (_disposed || !armed.Ask(this))
            {
                armed.Dispose();
                return false;
            }

            _armed?.Dispose();
            _armed = armed;
            return true;
        }
    }

    /// <summary>On a pool thread: re-armed first, then told. Nothing may escape into the pool's wait.</summary>
    private void Heard(Armed which)
    {
        try
        {
            bool tell;
            lock (_steps)
            {
                lock (_gate)
                {
                    if (_disposed || !ReferenceEquals(which, _armed))
                    {
                        return;
                    }
                }

                var was = _at;
                (_at, tell) = KeyFollow.Heard(was, this);
                if (_at == KeyWatched.Nothing && was != KeyWatched.Nothing)
                {
                    Log.Warn(Scope, $"stopped following {_hive}\\{_path}: changes made elsewhere will show on the next look");
                }
            }

            if (tell)
            {
                _changed();
            }
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
    private sealed class Armed(RegistryKey key, bool subtree) : IDisposable
    {
        private readonly AutoResetEvent _signal = new(false);
        private RegisteredWaitHandle? _wait;

        /// <summary>Asks for the next change of the key; false where Windows refused (the key was deleted).</summary>
        public bool Ask(RegistryWatch owner)
        {
            _wait?.Unregister(null);
            _wait = null;

            var filter = ToggleNative.RegNotifyName | ToggleNative.RegNotifyLastSet | ToggleNative.RegNotifyThreadAgnostic;
            if (ToggleNative.RegNotifyChangeKeyValue(key.Handle, subtree, filter, _signal.SafeWaitHandle, asynchronous: true) != 0)
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
