using Microsoft.UI.Dispatching;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Windows.Devices.Geolocation;
using Windows.Foundation;

namespace Themia.App.Services;

/// <summary>
/// [S-2851] [S-2852] Windows' location, shared by every widget that uses it: one locator for the app,
/// started only while some widget's Use my location switch is on (<see cref="SetWanted"/>) and gone
/// when none is. Access is asked once per start (<c>Geolocator.RequestAccessAsync</c>, which shows
/// Windows' own prompt where it has one), the first position read once (Windows' cached fix if it is
/// recent), and after that the locator speaks only when the device has moved
/// (<see cref="DeviceLocationRules.MovementThresholdMeters"/>) or its status changes (location turned
/// off or on in Settings): event-driven, no timer, no polling. What it hears goes through
/// <see cref="DeviceLocationRules"/>, so the widgets only ever see a position rounded to about a
/// kilometre, and a new one only after a move of <see cref="DeviceLocationRules.MoveKm"/>.
/// <para>
/// Privacy: no coordinate is logged, at any precision; the log says states only. Nothing is written
/// to disk: the widgets' configs hold the switch, and the sync's shelf holds the weather at the
/// rounded place as it holds any place's.
/// </para>
/// <para>
/// Every WinRT callback catches, logs and returns (nothing escapes a native callback); the
/// answers are applied on the UI thread, in order, and <see cref="Changed"/> is raised there.
/// </para>
/// </summary>
internal sealed class DeviceLocationService : IDisposable
{
    private const string Scope = "location";

    /// <summary>How old Windows' own cached fix may be for the first position; Windows reads afresh past it.</summary>
    private static readonly TimeSpan FirstFixAge = TimeSpan.FromMinutes(30);

    /// <summary>The most the first position is waited for before it counts as unavailable (a Windows timeout, not a sleep).</summary>
    private static readonly TimeSpan FirstFixTimeout = TimeSpan.FromSeconds(30);

    private readonly DispatcherQueue _dispatcher;
    private DeviceLocation _current = DeviceLocation.Off;
    private Geolocator? _locator;
    private TypedEventHandler<Geolocator, PositionChangedEventArgs>? _moved;
    private TypedEventHandler<Geolocator, StatusChangedEventArgs>? _status;

    /// <summary>Bumped on every start and stop: an answer from an earlier start is dropped.</summary>
    private int _generation;

    public DeviceLocationService(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>What the widgets see now. UI thread.</summary>
    public DeviceLocation Current => _current;

    /// <summary>Raised on the UI thread when <see cref="Current"/> changed.</summary>
    public event Action? Changed;

    /// <summary>
    /// [S-2851] Whether any widget uses the device's location (<c>DeviceLocationInterest.Wanted</c>),
    /// on every state change: the locator starts on the first, stops (and the position is forgotten)
    /// on the last. Single-flight: a start already running is not started again. UI thread.
    /// </summary>
    public void SetWanted(bool wanted)
    {
        var was = _current.State != DeviceLocationState.Off;
        if (wanted == was)
        {
            return;
        }

        Publish(DeviceLocationRules.Wanted(_current, wanted));
        if (wanted)
        {
            _ = StartAsync(++_generation);
        }
        else
        {
            _generation++;
            StopLocator();
            Log.Info(Scope, "no widget uses the device's location; the locator is stopped");
        }
    }

    /// <summary>[S-2851] Windows' location privacy page, where location is turned on for the device and for Themia.</summary>
    public static void OpenSettings() => _ = ShellLauncher.OpenWindowsSettings(new Uri("ms-settings:privacy-location"));

    public void Dispose()
    {
        _generation++;
        StopLocator();
    }

    private async Task StartAsync(int generation)
    {
        try
        {
            // On the UI thread: where Windows asks the user, it asks from the app's window.
            var access = await Geolocator.RequestAccessAsync();
            if (generation != _generation)
            {
                return;
            }

            var allowed = access == GeolocationAccessStatus.Allowed;
            Log.Info(Scope, $"access to the device's location: {access}");
            Publish(DeviceLocationRules.Access(_current, allowed ? DeviceAccess.Allowed : DeviceAccess.Denied));

            // The locator is made either way: its status says when location is turned on or off in
            // Settings, so a denied start recovers on its own the moment the user allows it.
            var locator = new Geolocator
            {
                DesiredAccuracy = PositionAccuracy.Default,
                MovementThreshold = DeviceLocationRules.MovementThresholdMeters,
            };
            _moved = (_, e) => OnPosition(generation, e);
            _status = (_, e) => OnStatus(generation, e);
            locator.PositionChanged += _moved;
            locator.StatusChanged += _status;
            _locator = locator;

            if (allowed)
            {
                await FirstFixAsync(locator, generation);
            }
        }
        catch (Exception ex)
        {
            // An async start from a state change: nothing may escape. No location stack (a server SKU).
            Log.Warn(Scope, $"the device's location could not be started: {ex.Message}");
            if (generation == _generation)
            {
                Publish(DeviceLocationRules.Failed(_current));
            }
        }
    }

    private async Task FirstFixAsync(Geolocator locator, int generation)
    {
        try
        {
            var fix = await locator.GetGeopositionAsync(FirstFixAge, FirstFixTimeout);
            if (generation == _generation)
            {
                var point = fix.Coordinate.Point.Position;
                Publish(DeviceLocationRules.Position(_current, point.Latitude, point.Longitude));
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Turned off between the access answer and the read: the status event says the same.
            if (generation == _generation)
            {
                Publish(DeviceLocationRules.Access(_current, DeviceAccess.Denied));
            }
        }
        catch (Exception ex)
        {
            // A timeout or no fix to give: unavailable until the locator reports a position.
            Log.Info(Scope, $"no first position from Windows: {ex.Message}");
            if (generation == _generation)
            {
                Publish(DeviceLocationRules.Failed(_current));
            }
        }
    }

    /// <summary>A WinRT callback on a worker thread: read there, applied on the UI thread.</summary>
    private void OnPosition(int generation, PositionChangedEventArgs e)
    {
        try
        {
            var point = e.Position.Coordinate.Point.Position;
            var (latitude, longitude) = (point.Latitude, point.Longitude);
            Apply(generation, () => Publish(DeviceLocationRules.Position(_current, latitude, longitude)));
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, $"a position from Windows could not be read: {ex.Message}");
        }
    }

    private void OnStatus(int generation, StatusChangedEventArgs e)
    {
        try
        {
            var status = e.Status switch
            {
                PositionStatus.Disabled => DeviceStatus.Disabled,
                PositionStatus.NotAvailable => DeviceStatus.NotAvailable,
                _ => DeviceStatus.Working,
            };
            Log.Info(Scope, $"Windows' location says {e.Status}");
            Apply(generation, () =>
            {
                var was = _current.State;
                Publish(DeviceLocationRules.Status(_current, status));

                // Turned back on in Settings: read a position at once rather than wait for a move.
                if (was is DeviceLocationState.Denied or DeviceLocationState.Unavailable && _current.State == DeviceLocationState.Locating && _locator is { } locator)
                {
                    _ = FirstFixAsync(locator, generation);
                }
            });
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, $"a status from Windows could not be read: {ex.Message}");
        }
    }

    private void Apply(int generation, Action change)
    {
        if (!_dispatcher.TryEnqueue(() =>
        {
            try
            {
                if (generation == _generation)
                {
                    change();
                }
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "the device's location could not be applied", ex);
            }
        }))
        {
            Log.Warn(Scope, "the UI thread is gone; a location answer was dropped");
        }
    }

    private void Publish(DeviceLocation next)
    {
        if (next == _current)
        {
            return;
        }

        var state = next.State;
        var moved = next.IsLocated && _current.IsLocated;
        _current = next;

        // States only: never a coordinate, at any precision.
        Log.Info(Scope, moved ? "the device moved; a new position (rounded) is in use" : $"device location: {state}");
        Changed?.Invoke();
    }

    private void StopLocator()
    {
        if (_locator is { } locator)
        {
            try
            {
                if (_moved is not null)
                {
                    locator.PositionChanged -= _moved;
                }

                if (_status is not null)
                {
                    locator.StatusChanged -= _status;
                }
            }
            catch (Exception ex)
            {
                Log.Warn(Scope, $"the locator could not be let go: {ex.Message}");
            }
        }

        _locator = null;
        _moved = null;
        _status = null;
    }
}
