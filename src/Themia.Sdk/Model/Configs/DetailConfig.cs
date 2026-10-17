namespace Themia.Core.Model;

/// <summary>
/// [S-2400] What every system detail widget shares. It reads on the hub's one cadence ([S-2503]) and
/// draws no history (that is the meter's, [S-1762]), so it has no timing or chart settings.
/// </summary>
public abstract class DetailConfig : WidgetConfig
{
    /// <summary>Nothing shared can be out of range. Kinds append their own repairs.</summary>
    public override IReadOnlyList<string> Repair() => [];
}
