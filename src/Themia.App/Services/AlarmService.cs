using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.Win32.SafeHandles;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Services;

/// <summary>
/// [S-4623]..[S-4627] The alarm of every alarm widget, rung whether or not a widget shows it: one
/// for the app's lifetime, started by the host. It keeps the <see cref="AlarmBook"/>, synced from the
/// state on every change, and <b>one</b> waitable timer set to the next thing due (a ring, a snooze
/// over, a ring that has run long enough) — an absolute UTC due time, which Windows keeps through a
/// clock change, and fires on waking when it passed during sleep. It is set again after every tick,
/// on <c>WM_TIMECHANGE</c> (the zone cache cleared: the next 07:00 is another instant) and on resume.
/// No polling: with no alarm on, nothing is set.
/// <para>
/// A ring plays the widget's sound looped (<see cref="CuePlayer"/>, the timer's ringtones) and shows a
/// Windows notification with the app's identity — the alarm scenario, Snooze and Dismiss buttons,
/// silent itself since the sound is ours. Alarms ring only while Themia runs.
/// </para>
/// </summary>
internal sealed partial class AlarmService : IAlarms, IDisposable
{
    private const string Scope = "alarms";
    private const string Group = "alarms";

    private readonly DispatcherQueue _dispatcher;
    private readonly Func<AppState> _state;
    private readonly Action _save;
    private readonly AlarmBook _book = new();
    private readonly SafeWaitHandle _timer;
    private readonly TimerHandle? _waitHandle;
    private readonly RegisteredWaitHandle? _wait;
    private bool _notifications;
    private bool _disposed;

    /// <param name="state">The live state, read on every sync.</param>
    /// <param name="save">Persist the state without a relayout: a one-shot alarm switched itself off.</param>
    public AlarmService(DispatcherQueue dispatcher, Func<AppState> state, Action save)
    {
        _dispatcher = dispatcher;
        _state = state;
        _save = save;

        _book.Rang += OnRang;
        _book.Silenced += OnSilenced;
        _book.Switched += _ => _save();

        // A synchronization timer: one signal per due time, reset by the wait it satisfies.
        _timer = CreateWaitableTimerExW(0, 0, 0, TimerAllAccess);
        if (_timer.IsInvalid)
        {
            Log.Error(Scope, $"CreateWaitableTimerEx failed (win32 {Marshal.GetLastWin32Error()}); alarms will not ring");
            return;
        }

        _waitHandle = new TimerHandle(_timer);
        _wait = ThreadPool.RegisterWaitForSingleObject(_waitHandle, OnSignalled, null, Timeout.Infinite, executeOnlyOnce: false);
    }

    /// <summary>What an alarm is doing changed (rang, silenced, synced): views draw again. UI thread.</summary>
    public event Action? Changed;

    /// <summary>The zone alarms are reckoned in.</summary>
    public TimeZoneInfo Zone => _book.Zone;

    /// <summary>Start: listen for notification buttons and take the alarms there are.</summary>
    public void Start()
    {
        RegisterNotifications();
        Sync();
    }

    /// <summary>What the alarm of widget <paramref name="widgetId"/> is doing now.</summary>
    public AlarmStatus Status(string widgetId) => _book.Of(widgetId, DateTime.UtcNow);

    /// <summary>
    /// [S-4623] The alarms of every view again (a settings change, a switch on a widget, a widget
    /// added, deleted or brought back by an undo). UI thread.
    /// </summary>
    public void Sync()
    {
        if (_disposed)
        {
            return;
        }

        var now = DateTime.UtcNow;
        _book.Sync(AlarmSlot.Of(_state().Views), now);
        _book.Tick(now);
        Arm();
        Changed?.Invoke();
    }

    /// <summary>[S-4625] Snooze a ringing alarm.</summary>
    public void Snooze(string key)
    {
        if (_book.Snooze(key, DateTime.UtcNow))
        {
            Arm();
            Changed?.Invoke();
        }
    }

    /// <summary>[S-4625] Dismiss a ringing or snoozed alarm.</summary>
    public void Dismiss(string key)
    {
        if (_book.Dismiss(key))
        {
            Arm();
            Changed?.Invoke();
        }
    }

    /// <summary>[S-4623] The clock or the zone changed: the next 07:00 is another instant.</summary>
    public void TimeChanged()
    {
        TimeZoneInfo.ClearCachedData();
        Due();
    }

    /// <summary>[S-4623] The PC woke: what came due while it slept rings now (within the grace) or is missed.</summary>
    public void Resumed() => Due();

    // ---- the timer --------------------------------------------------------------------------

    /// <summary>Look at every alarm now, then set the timer to the next thing due.</summary>
    private void Due()
    {
        if (_disposed)
        {
            return;
        }

        if (_book.Tick(DateTime.UtcNow))
        {
            Changed?.Invoke();
        }

        Arm();
    }

    /// <summary>Set the one timer to the next due time, or cancel it when nothing is on.</summary>
    private unsafe void Arm()
    {
        if (_disposed || _timer.IsInvalid)
        {
            return;
        }

        if (_book.Due(DateTime.UtcNow) is not { } due)
        {
            // ignore: cancelling a timer that is not set is not an error worth a line.
            _ = CancelWaitableTimer(_timer);
            return;
        }

        // Positive: an absolute time in UTC FILETIME units, which follows Windows' clock.
        var at = due.ToFileTimeUtc();
        if (!SetWaitableTimer(_timer, &at, 0, 0, 0, false))
        {
            Log.Error(Scope, $"SetWaitableTimer failed (win32 {Marshal.GetLastWin32Error()})");
        }
    }

    // A pool thread: only a hand-off, nothing may escape.
    private void OnSignalled(object? state, bool timedOut)
    {
        try
        {
            if (!_disposed && !_dispatcher.TryEnqueue(Due))
            {
                Log.Debug(Scope, "an alarm came due while the dispatcher was shutting down");
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "handing an alarm to the UI thread failed", ex);
        }
    }

    // ---- ringing ----------------------------------------------------------------------------

    private void OnRang(string key)
    {
        if (_book.Slot(key) is not { Alarm: var alarm })
        {
            return;
        }

        Log.Info(Scope, "an alarm is ringing");

        if (alarm.Sound != AlarmSound.Off)
        {
            CuePlayer.PlayLooping(PomodoroSounds.Wave(alarm.Sound, PomodoroPhase.Work, alarm.EffectiveVolume));
        }

        ShowNotification(key, alarm);
    }

    private void OnSilenced(string key)
    {
        if (!_book.AnyRinging)
        {
            CuePlayer.Stop();
        }

        RemoveNotification(key);
    }

    // ---- the notification ------------------------------------------------------------------

    private void RegisterNotifications()
    {
        try
        {
            var manager = AppNotificationManager.Default;
            manager.NotificationInvoked += OnNotificationInvoked;

            if (PackageIdentity.IsPackaged)
            {
                manager.Register();
            }
            else
            {
                // Unpackaged, the name and icon come with the registration (the package's, packaged).
                var icon = Path.Combine(AppContext.BaseDirectory, "Assets", "themia.ico");
                manager.Register("Themia", new Uri(icon));
            }

            _notifications = true;
        }
        catch (Exception ex)
        {
            // The ring still sounds; only the notice is missing.
            Log.Error(Scope, "could not register for notifications; alarms ring without a notice", ex);
        }
    }

    private void ShowNotification(string key, IAlarm alarm)
    {
        if (!_notifications)
        {
            return;
        }

        try
        {
            var locale = Strings.Current;
            var notification = new AppNotificationBuilder()
                .SetTag(Tag(key))
                .SetGroup(Group)
                .SetScenario(AppNotificationScenario.Alarm)
                .AddText(AlarmWords.Name(locale, alarm))
                .AddText(AlarmWords.Time(locale, alarm.Plan))
                .AddButton(new AppNotificationButton(Strings.Get(locale, Str.Alarm_Snooze)).AddArgument("action", "snooze").AddArgument("key", key))
                .AddButton(new AppNotificationButton(Strings.Get(locale, Str.Alarm_Dismiss)).AddArgument("action", "dismiss").AddArgument("key", key))
                .MuteAudio()
                .BuildNotification();

            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "could not show the alarm's notice", ex);
        }
    }

    private void RemoveNotification(string key)
    {
        if (!_notifications)
        {
            return;
        }

        _ = RemoveAsync(key);
    }

    private static async Task RemoveAsync(string key)
    {
        try
        {
            await AppNotificationManager.Default.RemoveByTagAndGroupAsync(Tag(key), Group);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not take the alarm's notice away", ex);
        }
    }

    /// <summary>A tag is at most 64 characters: the key's hash, stable for the run.</summary>
    private static string Tag(string key) => ((uint)StringComparer.Ordinal.GetHashCode(key)).ToString("x8", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// A button on the notice, from Windows' thread. Its arguments cross a trust boundary: only the two
    /// actions are taken, and only for a key the book holds.
    /// </summary>
    private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        try
        {
            var arguments = args.Arguments;
            if (!arguments.TryGetValue("action", out var action) || !arguments.TryGetValue("key", out var key))
            {
                return;
            }

            _ = _dispatcher.TryEnqueue(() =>
            {
                try
                {
                    if (_book.Slot(key) is null)
                    {
                        Log.Warn(Scope, "a notice asked about an alarm that is gone");
                        return;
                    }

                    switch (action)
                    {
                        case "snooze":
                            Snooze(key);
                            break;
                        case "dismiss":
                            Dismiss(key);
                            break;
                        default:
                            Log.Warn(Scope, "a notice asked for an action alarms do not have");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(Scope, "acting on an alarm's notice failed", ex);
                }
            });
        }
        catch (Exception ex)
        {
            // A COM callback: nothing may escape it.
            Log.Error(Scope, "reading an alarm's notice failed", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _wait?.Unregister(null);
        _waitHandle?.Dispose();
        _timer.Dispose();
        CuePlayer.Stop();

        if (_notifications)
        {
            try
            {
                AppNotificationManager.Default.NotificationInvoked -= OnNotificationInvoked;
                AppNotificationManager.Default.Unregister();
            }
            catch (Exception ex)
            {
                Log.Warn(Scope, "could not unregister from notifications", ex);
            }
        }
    }

    /// <summary>The timer as a wait handle for the pool's wait; it does not own the handle.</summary>
    private sealed class TimerHandle : WaitHandle
    {
        public TimerHandle(SafeWaitHandle handle)
        {
            SafeWaitHandle = new SafeWaitHandle(handle.DangerousGetHandle(), ownsHandle: false);
        }
    }

    private const uint TimerAllAccess = 0x1F0003;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial SafeWaitHandle CreateWaitableTimerExW(nint attributes, nint name, uint flags, uint access);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool SetWaitableTimer(SafeWaitHandle timer, long* dueTime, int period, nint completion, nint argument, [MarshalAs(UnmanagedType.Bool)] bool resume);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CancelWaitableTimer(SafeWaitHandle timer);
}
