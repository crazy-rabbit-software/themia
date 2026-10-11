using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>One active output (or input) device, as Windows names it in Sound settings.</summary>
/// <param name="Id">The endpoint identifier: stable across restarts and reconnects of the same device.</param>
/// <param name="Name">The friendly name ("Speakers (Realtek(R) Audio)"), or empty when it could not be read.</param>
public sealed record AudioDevice(string Id, string Name);

/// <summary>How a widget's chosen device resolved against the devices that are there right now.</summary>
public enum AudioTarget
{
    /// <summary>No device chosen: the widget follows Windows' default output.</summary>
    Default,

    /// <summary>The chosen device is connected.</summary>
    Chosen,

    /// <summary>The chosen device is not connected (unplugged, disabled). Never silently the default.</summary>
    Missing,
}

/// <summary>What a key press asks of an audio widget.</summary>
public enum AudioKeyKind
{
    None,

    /// <summary>Move the row cursor to <see cref="AudioKeyAction.Index"/> (mixer only).</summary>
    Move,

    /// <summary>Change the level by <see cref="AudioKeyAction.Delta"/> whole percentage points.</summary>
    Step,

    ToggleMute,

    /// <summary>Drop the row cursor (Escape, or the keyboard lease ending).</summary>
    Clear,

    /// <summary>Open the focused row's context menu.</summary>
    Menu,
}

/// <summary>A key's meaning for an audio widget.</summary>
public readonly record struct AudioKeyAction(AudioKeyKind Kind, int Index = 0, int Delta = 0)
{
    public static AudioKeyAction None { get; } = new(AudioKeyKind.None);
}

/// <summary>
/// [S-1221]..[S-1234] The rules the mixer and the volume tile share beyond the level arithmetic in
/// <see cref="VolumeRules"/>: which device a widget is bound to, what a key does, how a key steps a
/// level, and how per-session peaks become per-row peaks.
/// <para>
/// Kept out of the COM code so it can be tested without an audio device — CI has none.
/// </para>
/// </summary>
public static class AudioRules
{
    /// <summary>[S-1234] Percentage points an arrow (or +/−) moves a level; what the volume flyout steps by.</summary>
    public const int ArrowStep = 2;

    /// <summary>[S-1234] Percentage points Page Up / Page Down move a level.</summary>
    public const int PageStep = 10;

    /// <summary>
    /// [S-1221] The endpoint a widget binds to. No choice follows the default (and is
    /// <see cref="AudioTarget.Missing"/> only when there is no default at all). A chosen device that is
    /// not connected stays missing rather than falling back to the default: a tile labelled for the
    /// headphones that quietly drove the speakers would move the wrong volume.
    /// </summary>
    public static (string? Id, AudioTarget Target) Resolve(string? chosen, IReadOnlyList<AudioDevice> devices, string? defaultId)
    {
        ArgumentNullException.ThrowIfNull(devices);

        if (string.IsNullOrEmpty(chosen))
        {
            return string.IsNullOrEmpty(defaultId) ? (null, AudioTarget.Missing) : (defaultId, AudioTarget.Default);
        }

        foreach (var device in devices)
        {
            if (string.Equals(device.Id, chosen, StringComparison.OrdinalIgnoreCase))
            {
                return (device.Id, AudioTarget.Chosen);
            }
        }

        return (null, AudioTarget.Missing);
    }

    /// <summary>
    /// [S-1221] The name to show for a device: its friendly name, else the name stored when it was
    /// chosen (so an unplugged device is still called by name), else a neutral placeholder.
    /// </summary>
    public static string DeviceName(string? id, IReadOnlyList<AudioDevice> devices, string? remembered, Locale? locale = null)
    {
        ArgumentNullException.ThrowIfNull(devices);

        foreach (var device in devices)
        {
            if (string.Equals(device.Id, id, StringComparison.OrdinalIgnoreCase) && device.Name.Trim().Length > 0)
            {
                return device.Name.Trim();
            }
        }

        return string.IsNullOrWhiteSpace(remembered) ? Strings.Get(locale ?? Strings.Current, Str.WidgetsA_Audio_UnnamedDevice) : remembered.Trim();
    }

    /// <summary>
    /// [S-1234] A level moved by whole percentage points, landing on the whole-percent grid the slider
    /// and the readout use, clamped to 0..1. Stepping from 37.4% by +2 gives 39%, not 39.4%.
    /// </summary>
    public static double Step(double level, int deltaPercent)
    {
        var percent = (int)Math.Round(MixerRules.ClampLevel(level) * 100, MidpointRounding.AwayFromZero);
        return Math.Clamp(percent + deltaPercent, 0, 100) / 100.0;
    }

    /// <summary>[S-1226] The volume tile's keys: arrows and +/− step, Page Up/Down step further, M or Space mute.</summary>
    public static AudioKeyAction VolumeKey(WidgetKey key) => key switch
    {
        WidgetKey.Right or WidgetKey.Up or WidgetKey.Plus => new(AudioKeyKind.Step, Delta: ArrowStep),
        WidgetKey.Left or WidgetKey.Down or WidgetKey.Minus => new(AudioKeyKind.Step, Delta: -ArrowStep),
        WidgetKey.PageUp => new(AudioKeyKind.Step, Delta: PageStep),
        WidgetKey.PageDown => new(AudioKeyKind.Step, Delta: -PageStep),
        WidgetKey.Mute or WidgetKey.Space => new(AudioKeyKind.ToggleMute),
        _ => AudioKeyAction.None,
    };

    /// <summary>
    /// [S-1225] The mixer's keys. Up/Down/Home/End move the row cursor; Left/Right and +/− step the
    /// focused row, Page Up/Down step it further; M or Space mute it; the Menu key opens its menu;
    /// Escape lets go. With no row focused yet, a key that would act on a row focuses the first one
    /// instead of acting: the first key press shows where the cursor is before anything changes.
    /// </summary>
    public static AudioKeyAction MixerKey(WidgetKey key, int? focused, int count)
    {
        if (count <= 0)
        {
            return key == WidgetKey.Escape ? new(AudioKeyKind.Clear) : AudioKeyAction.None;
        }

        int? at = focused is { } index && index >= 0 && index < count ? index : null;

        switch (key)
        {
            case WidgetKey.Escape:
                return new(AudioKeyKind.Clear);

            case WidgetKey.Up or WidgetKey.Down or WidgetKey.Home or WidgetKey.End:
                return ItemCursor.Move(count, at, key) is { } next ? new(AudioKeyKind.Move, next) : AudioKeyAction.None;

            case WidgetKey.PageUp or WidgetKey.PageDown or WidgetKey.Left or WidgetKey.Right
                or WidgetKey.Plus or WidgetKey.Minus or WidgetKey.Mute or WidgetKey.Space or WidgetKey.Menu:
                if (at is not { } row)
                {
                    return new(AudioKeyKind.Move, 0);
                }

                if (key == WidgetKey.Menu)
                {
                    return new(AudioKeyKind.Menu, row);
                }

                return VolumeKey(key) with { Index = row };

            default:
                return AudioKeyAction.None;
        }
    }

    /// <summary>A meter reading as a 0..1 fraction; a junk value from across the COM boundary is silence.</summary>
    public static double Peak(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;

    /// <summary>
    /// [S-1223] Per-session peaks keyed both ways a row can be keyed: by the session's own key (an
    /// ungrouped row) and by its group key (a grouped row, which shows its loudest session).
    /// </summary>
    public static Dictionary<string, double> PeaksByKey(IEnumerable<(string SessionKey, string GroupKey, double Peak)> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        var peaks = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (sessionKey, groupKey, value) in sessions)
        {
            var peak = Peak(value);
            peaks[sessionKey] = peak;
            peaks[groupKey] = peaks.TryGetValue(groupKey, out var loudest) ? Math.Max(loudest, peak) : peak;
        }

        return peaks;
    }

    /// <summary>
    /// [S-1223] Whether a new set of peaks is worth sending to the views. A silent machine samples
    /// zero ten times a second; repainting zero over zero is work for nothing, so silence is sent
    /// once and then not again until something moves.
    /// </summary>
    public static bool PeaksWorthSending(bool previousSilent, double output, IReadOnlyDictionary<string, double> byKey)
    {
        ArgumentNullException.ThrowIfNull(byKey);

        if (!previousSilent)
        {
            return true;
        }

        return output > 0 || byKey.Values.Any(static peak => peak > 0);
    }
}
