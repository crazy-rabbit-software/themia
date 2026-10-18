using System.Text.Json.Serialization;
using Themia.Core.Office;

namespace Themia.Core.Model;

/// <summary>[S-6074] The sound rows of the pomodoro and the alarm that depend on the sound chosen.</summary>
public enum SoundSetting
{
    /// <summary>The volume: only while a sound is chosen.</summary>
    Volume,
}

/// <summary>
/// [S-1460] The phase-flip cue: off, or one of the original's twelve ringtones. Read leniently, so
/// a hand-edited or unknown name is the default (<see cref="Chime"/>, the zero value) rather than a
/// state file that refuses to load.
/// </summary>
[JsonConverter(typeof(LenientEnumConverter<AlarmSound>))]
public enum AlarmSound
{
    Chime = 0,
    Marimba = 1,
    Crystal = 2,
    Pluck = 3,
    Droplet = 4,
    Birdsong = 5,
    Zen = 6,
    Bell = 7,
    Gong = 8,
    Arcade = 9,
    Digital = 10,
    Buzzer = 11,
    Off = 12,
}
