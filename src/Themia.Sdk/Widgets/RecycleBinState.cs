namespace Themia.Core.Widgets;

/// <summary>[S-1389] What <c>SHQueryRecycleBin</c> said: how many items, and how many bytes.</summary>
public readonly record struct RecycleBinState(long Items, long Bytes)
{
    public bool IsEmpty => Items <= 0;

    /// <summary>
    /// [S-4863] Whether Empty is offered: only with something in the bin (an unknown answer is not
    /// "something"), and not while an empty is already out. The widget's button and the Shortcuts
    /// item menu both ask this.
    /// </summary>
    public static bool CanEmpty(RecycleBinState? state, bool emptying) => state is { IsEmpty: false } && !emptying;
}
