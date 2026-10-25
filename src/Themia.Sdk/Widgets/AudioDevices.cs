using Themia.Core.Widgets;

namespace Themia.Sdk;

/// <summary>
/// [S-1222] The active output devices a properties menu lists (the volume widget's device choice): the
/// host's Core Audio enumeration, installed by the host once at start, as the place lookup is.
/// </summary>
public static class AudioDevices
{
    private static Func<Task<IReadOnlyList<AudioDevice>>>? _outputs;

    /// <summary>The active output devices, in Windows' order; empty when Core Audio would not answer.</summary>
    public static Task<IReadOnlyList<AudioDevice>> OutputsAsync()
        => (Volatile.Read(ref _outputs) ?? throw new InvalidOperationException("no audio devices installed: the host installs them before any menu is shown"))();

    /// <summary>The host's enumeration. Once per process.</summary>
    internal static void Install(Func<Task<IReadOnlyList<AudioDevice>>> outputs)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        if (Interlocked.CompareExchange(ref _outputs, outputs, null) is not null)
        {
            throw new InvalidOperationException("the audio devices are installed once per process");
        }
    }
}
