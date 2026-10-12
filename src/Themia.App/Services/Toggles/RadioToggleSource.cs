using Themia.Core.Diagnostics;
using Themia.Core.Widgets;
using Windows.Devices.Radios;
using Windows.Foundation;

namespace Themia.App.Services.Toggles;

/// <summary>
/// [S-3910] The Wi-Fi or Bluetooth radio through <c>Windows.Devices.Radios</c>, as Karpo's radio toggles
/// switch it (<c>RadioSwitch.cs</c>): found once on the pool, its <c>StateChanged</c> followed so a radio
/// switched in Windows' own panel shows at once, and switched with <c>SetStateAsync</c> after
/// <c>RequestAccessAsync</c>. No radio of the kind: no hardware. Windows refusing the switch (it may, to
/// an app): the click says so and the next one asks again.
/// <para>
/// The radio is only ever read on the pool or on its own event's thread (the multithreaded apartment
/// it came from), never on the UI thread: reading one apartment's object from another throws.
/// </para>
/// </summary>
internal sealed class RadioToggleSource(RadioKind kind) : ToggleSource
{
    private readonly Lock _gate = new();
    private Radio? _radio;
    private TypedEventHandler<Radio, object>? _handler;

    public override void Start() => _ = Task.Run(FindAsync);

    private async Task FindAsync()
    {
        try
        {
            Radio? found = null;
            foreach (var radio in await Radio.GetRadiosAsync())
            {
                if (radio.Kind == kind)
                {
                    found = radio;
                    break;
                }
            }

            if (found is null)
            {
                Publish(ToggleReading.Blocked(ToggleBlock.NoHardware));
                return;
            }

            TypedEventHandler<Radio, object> handler = (sender, _) => Note(sender);
            lock (_gate)
            {
                if (Disposed)
                {
                    return;
                }

                _radio = found;
                _handler = handler;
            }

            found.StateChanged += handler;
            Note(found);
        }
        catch (Exception ex)
        {
            // No radio stack at all (a VM, a server SKU): nothing to switch.
            Log.Warn(Scope, $"the {kind} radio could not be found: {ex.Message}");
            Publish(ToggleReading.Blocked(ToggleBlock.NoHardware));
        }
    }

    /// <summary>Read where the radio stands; here, on its own thread. Never throws (an event handler).</summary>
    private void Note(Radio radio)
    {
        try
        {
            Publish(radio.State switch
            {
                RadioState.On => ToggleReading.Of(true),
                RadioState.Off => ToggleReading.Of(false),

                // Disabled: a hardware switch or the driver holds it off; Windows' own tile cannot switch it either.
                RadioState.Disabled => ToggleReading.Blocked(ToggleBlock.TurnedOff),
                _ => ToggleReading.Pending,
            });
        }
        catch (Exception ex)
        {
            // A radio going away while it is read is what switching one off can look like.
            Log.Debug(Scope, $"the {kind} radio could not be read: {ex.Message}");
            Publish(ToggleReading.Pending);
        }
    }

    /// <inheritdoc />
    /// <remarks>On the pool, as every call on the radio is.</remarks>
    public override Task<bool> SetAsync(bool on) => Task.Run(() => SwitchAsync(on));

    private async Task<bool> SwitchAsync(bool on)
    {
        Radio? radio;
        lock (_gate)
        {
            radio = _radio;
        }

        if (radio is null)
        {
            return false;
        }

        try
        {
            if (await Radio.RequestAccessAsync() != RadioAccessStatus.Allowed)
            {
                Log.Warn(Scope, $"Windows did not let Themia switch the {kind} radio");
                return false;
            }

            var answer = await radio.SetStateAsync(on ? RadioState.On : RadioState.Off);
            Note(radio);
            if (answer != RadioAccessStatus.Allowed)
            {
                Log.Warn(Scope, $"switching the {kind} radio answered {answer}");
            }

            return answer == RadioAccessStatus.Allowed;
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"the {kind} radio could not be switched", ex);
            return false;
        }
    }

    protected override void Stop()
    {
        Radio? radio;
        TypedEventHandler<Radio, object>? handler;
        lock (_gate)
        {
            (radio, handler) = (_radio, _handler);
            (_radio, _handler) = (null, null);
        }

        if (radio is not null && handler is not null)
        {
            // Off the UI thread: the radio belongs to the pool's apartment.
            _ = Task.Run(() =>
            {
                try
                {
                    radio.StateChanged -= handler;
                }
                catch (Exception ex)
                {
                    Log.Debug(Scope, $"the {kind} radio's handler could not be removed: {ex.Message}");
                }
            });
        }
    }
}
