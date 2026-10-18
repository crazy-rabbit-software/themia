using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;

namespace Themia.App.Services;

/// <summary>
/// [S-153] Plays a synthesised cue — a WAVE file image built in Core — through
/// <c>winmm!PlaySound</c> from memory.
/// <para>
/// <c>PlaySound</c> rather than a <c>MediaPlayer</c> or an <c>AudioGraph</c>: those are whole media
/// pipelines, initialised asynchronously and rooted by the AOT compiler in full, to play two seconds
/// of oscillators. This is one P/Invoke.
/// </para>
/// <para>
/// <b>Single-flight</b> ([S-1462]): a new cue stops the one still sounding first, so dragging the
/// volume slider or flicking through the sound list never stacks cues on top of each other. The
/// wave is copied to native memory and played asynchronously; the copy lives exactly as long as it
/// can be read — until the next cue stops it — so at most one buffer is ever outstanding, and the
/// GC never has to pin anything.
/// </para>
/// </summary>
internal static unsafe partial class CuePlayer
{
    private const string Scope = "sound-cue";

    private const uint SndAsync = 0x0001;

    /// <summary>Never substitute the default beep: a failed cue is better than the wrong one.</summary>
    private const uint SndNoDefault = 0x0002;

    private const uint SndMemory = 0x0004;

    /// <summary>[S-4626] Play until stopped: the alarm's ring.</summary>
    private const uint SndLoop = 0x0008;

    private static readonly Lock Gate = new();

    /// <summary>The buffer the sound driver may still be reading, or null.</summary>
    private static void* _current;

    /// <summary>Play <paramref name="wave"/>, stopping any cue still sounding. An empty wave only stops.</summary>
    public static void Play(byte[] wave) => Play(wave, loop: false);

    /// <summary>[S-4626] Play <paramref name="wave"/> over and over until <see cref="Stop"/> or the next cue: an alarm ringing.</summary>
    public static void PlayLooping(byte[] wave) => Play(wave, loop: true);

    /// <summary>Stop whatever is sounding.</summary>
    public static void Stop()
    {
        lock (Gate)
        {
            try
            {
                StopLocked();
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "stopping a cue threw", ex);
            }
        }
    }

    private static void Play(byte[] wave, bool loop)
    {
        ArgumentNullException.ThrowIfNull(wave);

        lock (Gate)
        {
            try
            {
                StopLocked();

                if (wave.Length == 0)
                {
                    return;
                }

                var buffer = NativeMemory.Alloc((nuint)wave.Length);
                wave.AsSpan().CopyTo(new Span<byte>(buffer, wave.Length));

                if (PlaySound((nint)buffer, 0, SndMemory | SndAsync | SndNoDefault | (loop ? SndLoop : 0)))
                {
                    _current = buffer;
                    return;
                }

                NativeMemory.Free(buffer);
                Log.Warn(Scope, "PlaySound refused the cue");
            }
            catch (Exception ex)
            {
                // A silent cue is a nuisance; an exception out of a timer tick or a settings click is a crash.
                Log.Error(Scope, "playing a cue threw", ex);
            }
        }
    }

    /// <summary>
    /// Stop whatever is sounding, then free its buffer. <c>PlaySound(NULL)</c> returns once the
    /// driver has stopped reading, which is what makes the free safe.
    /// </summary>
    private static void StopLocked()
    {
        if (_current is null)
        {
            return;
        }

        // ignore: the result only says whether something was playing, and either way it is not now.
        _ = PlaySound(0, 0, 0);
        NativeMemory.Free(_current);
        _current = null;
    }

    [LibraryImport("winmm.dll", EntryPoint = "PlaySoundW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PlaySound(nint data, nint module, uint flags);
}

/// <summary>[S-153] The player a widget's cue reaches through the SDK's <see cref="SoundCue"/>: this <see cref="CuePlayer"/>.</summary>
internal sealed class HostCuePlayer : ISoundCuePlayer
{
    public void Play(byte[] wave) => CuePlayer.Play(wave);
}
