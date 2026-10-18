using Themia.Core.Diagnostics;

namespace Themia.App.Services;

/// <summary>
/// [S-153] [S-1462] How a widget sounds a cue (a ringtone's wave, built by <c>PomodoroSounds</c>):
/// the host's one player, single-flight, so a new cue stops the one still sounding. The player is
/// Win32 (<c>winmm!PlaySound</c> from memory), so it stays host code behind this seam, installed
/// once at start; until then (a test, a tool) a cue logs and sounds nothing.
/// </summary>
public static class SoundCue
{
    private const string Scope = "sound-cue";

    private static ISoundCuePlayer? _player;

    /// <summary>Play <paramref name="wave"/> once, stopping any cue still sounding. An empty wave only stops.</summary>
    public static void Play(byte[] wave)
    {
        ArgumentNullException.ThrowIfNull(wave);
        if (Volatile.Read(ref _player) is { } player)
        {
            player.Play(wave);
        }
        else
        {
            Log.Debug(Scope, "no cue player installed; a cue was asked for and not sounded");
        }
    }

    /// <summary>The host's player. Once per process.</summary>
    internal static void Install(ISoundCuePlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (Interlocked.CompareExchange(ref _player, player, null) is not null)
        {
            throw new InvalidOperationException("the cue player is installed once per process");
        }
    }
}

/// <summary>The host's cue player behind <see cref="SoundCue"/>.</summary>
internal interface ISoundCuePlayer
{
    /// <summary>Play once, stopping any cue still sounding; never throws.</summary>
    void Play(byte[] wave);
}
