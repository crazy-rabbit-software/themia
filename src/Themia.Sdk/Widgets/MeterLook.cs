namespace Themia.App.Widgets.Meters;

/// <summary>
/// [S-6300] What a meter's face draws: the reading in one of its drawings, the history alone, or the
/// kind's items (cores, engines). The shared <c>MeterView</c> lays each out; a kind only says which
/// (<c>MeterPart.Look</c>). Pure, so the sizing rules (<see cref="Themia.Core.Widgets.MeterSizing"/>) read it.
/// </summary>
public enum MeterLook
{
    /// <summary>[S-1762] Legacy: the reading over its history (a kind not split yet).</summary>
    Graph = 0,

    /// <summary>[S-6302] The reading inside a half-circle gauge of the same share.</summary>
    Gauge = 1,

    /// <summary>[S-6303] A vertical bar per item (<c>MeterPart.WatchItems</c>).</summary>
    Columns = 2,

    /// <summary>[S-6304] A labelled bar row per item.</summary>
    Rows = 3,

    /// <summary>[S-6305] A ring per item.</summary>
    Tiles = 4,

    /// <summary>[S-6264] The reading and its second figure (upload under download), both large.</summary>
    Numbers = 5,

    /// <summary>[S-6262] A battery filled to the reading, the figure over it.</summary>
    Battery = 6,

    /// <summary>[S-6266] A ring filled to the reading, the figure inside it.</summary>
    Ring = 7,

    /// <summary>[S-6263] Signal bars by the reading's latency, the figure over them.</summary>
    Signal = 8,

    /// <summary>[S-6317] A cell per item, each the item's own history and figure.</summary>
    Graphs = 9,

    /// <summary>[S-11001] The reading alone: the number at 28 with its unit.</summary>
    Number = 10,

    /// <summary>[S-11002] The history alone, edge to edge, no number.</summary>
    History = 11,
}

/// <summary>[S-11000] Which of a subject's atoms a meter face is: its reading, its history, or its items.</summary>
public enum MeterAtom
{
    /// <summary>The reading, in one of its drawings (Number, Numbers, Gauge, Ring, Rows as a bar, Battery, Signal).</summary>
    Reading = 0,

    /// <summary>The history band alone, edge to edge.</summary>
    History = 1,

    /// <summary>The kind's items alone (cores, engines), in the card's insets.</summary>
    Items = 2,
}
