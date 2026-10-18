using System.Text.Json.Serialization;
using Themia.Core.Office;

namespace Themia.Core.Model;

/// <summary>
/// [S-1464] When the now-playing tile paints nothing. Read leniently: an unknown name is
/// <see cref="Never"/> (the zero value), the visible state — a widget you can see is one you can fix.
/// </summary>
[JsonConverter(typeof(LenientEnumConverter<MusicHideWhenIdle>))]
public enum MusicHideWhenIdle
{
    /// <summary>Always drawn; idle shows the "nothing playing" sentence.</summary>
    Never = 0,

    /// <summary>Hidden only while Windows reports no media session at all.</summary>
    NoSession = 1,

    /// <summary>Hidden whenever nothing is playing, a paused session included.</summary>
    NotPlaying = 2,
}
