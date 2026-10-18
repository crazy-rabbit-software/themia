using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-1389] [S-4863] The Recycle Bin as a widget asks for it (<see cref="IWidgetHost.RecycleBin"/>):
/// the host's one process-wide answer (how full the bin is, asked when the shell says it changed,
/// never on a clock) and Windows' own empty, so any number of widgets showing the bin cost one query
/// and agree on whether an empty is under way. UI thread throughout.
/// </summary>
public interface IRecycleBin
{
    /// <summary>The last answer, or null when none has come back yet.</summary>
    RecycleBinState? Current { get; }

    /// <summary>Windows is emptying the bin (its dialog may be up): a second empty is not offered.</summary>
    bool Emptying { get; }

    /// <summary>Raised on the UI thread when <see cref="Current"/> or <see cref="Emptying"/> changes.</summary>
    event Action? Changed;

    /// <summary>Show the bin: changes are followed from now on, a missing or stale answer is asked for once. Dispose to stop (twice is once).</summary>
    IDisposable Watch();

    /// <summary>Empty the bin on every drive the way Explorer does, Windows asking first. Does nothing unless an empty is offered; never throws.</summary>
    Task EmptyAsync();
}
