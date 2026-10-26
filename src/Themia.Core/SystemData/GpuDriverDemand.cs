namespace Themia.Core.SystemData;

/// <summary>What the GPU sampler does with the NVIDIA driver library before a sample ([S-437]).</summary>
public enum DriverStep
{
    /// <summary>Leave it as it is.</summary>
    None,

    /// <summary>Load and initialise it: a GPU widget is shown and the library is not open.</summary>
    Open,

    /// <summary>Shut it down and unload it: no GPU widget is shown any more.</summary>
    Close,
}

/// <summary>
/// [S-437] When the GPU sampler holds the NVIDIA management library: only while a GPU widget is
/// <b>shown</b> (a watch holds the hub's GPU feed: a widget on a view a display shows, or a render),
/// never for one that is only in a hidden view ([S-2513] keeps sampling those, with the counters'
/// figure). The library costs about 18 MB once initialised; its figure only matters for a load on
/// screen.
/// <para>
/// One per sampler session, asked by the session's sample before it reads (the samples are one at a
/// time, so the library is never opened or closed under a call in flight) and told how the step went.
/// A library that would not open is not tried again until no GPU widget has been shown for a sample:
/// a machine without NVIDIA hardware pays one failed look per time a GPU widget comes on screen, not
/// one per second. Not thread-safe: the session's single flight is its only caller.
/// </para>
/// </summary>
public sealed class GpuDriverDemand
{
    private bool _refused;

    /// <summary>Whether the library is open.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>The step to take before a sample, given whether any GPU widget is shown now.</summary>
    public DriverStep Before(bool shown)
    {
        if (!shown)
        {
            // Nothing on screen: a later showing may try again.
            _refused = false;
            return IsOpen ? DriverStep.Close : DriverStep.None;
        }

        return IsOpen || _refused ? DriverStep.None : DriverStep.Open;
    }

    /// <summary>How the <see cref="DriverStep.Open"/> went: a failure is not retried while still shown.</summary>
    public void Opened(bool open)
    {
        IsOpen = open;
        _refused = !open;
    }

    /// <summary>The library was shut down and unloaded (a <see cref="DriverStep.Close"/>, or the session ending).</summary>
    public void Closed() => IsOpen = false;

    /// <summary>The session is ending: close what is open.</summary>
    public DriverStep End() => IsOpen ? DriverStep.Close : DriverStep.None;
}
