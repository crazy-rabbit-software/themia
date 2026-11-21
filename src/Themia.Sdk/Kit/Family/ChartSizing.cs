namespace Themia.Sdk.Kit;

/// <summary>
/// [S-6755] The one sizing of a history band drawn the card's edge to edge ([S-6754]): the price's and the rate's history,
/// and the forecast's curve under its days. The band (the series' own room, what the chart writes over and under it apart)
/// is <see cref="BandLeast"/> at its least and <see cref="BandNatural"/> in a new card, <see cref="BandMost"/> at its stated
/// most (relaxed like every kind's, [S-12411]); a band card is <see cref="LeastWidth"/>..<see cref="MostWidth"/> wide, a new
/// one <see cref="NaturalWidth"/>. What a kind's chart keeps free over and under the band is the kind's own, added on top
/// (<see cref="Band"/>'s <c>keeps</c>): the chart only gets longer or taller, the band never shorter.
/// </summary>
public static class ChartSizing
{
    /// <summary>The band at its least: the huge icon's 48, a trend still readable.</summary>
    public const double BandLeast = FamilyShape.IconHuge;

    /// <summary>The band in a new card.</summary>
    public const double BandNatural = 72;

    /// <summary>The band at its stated most.</summary>
    public const double BandMost = 200;

    /// <summary>A band card's least width: a period's two ends written apart.</summary>
    public const double LeastWidth = 200;

    /// <summary>A new band card's width.</summary>
    public const double NaturalWidth = 280;

    /// <summary>A band card's stated most width.</summary>
    public const double MostWidth = 480;

    /// <summary>
    /// [S-6755] The sizing of a band chart that is the card's whole box: <see cref="LeastWidth"/>..<see cref="MostWidth"/> wide,
    /// as tall as the band and what its chart keeps free over and under it (<paramref name="keeps"/>), a picture that stretches.
    /// </summary>
    public static WidgetSizing Band(double keeps)
        => WidgetSizing.Range(
            new(LeastWidth, keeps + BandLeast),
            new(NaturalWidth, keeps + BandNatural),
            new(MostWidth, keeps + BandMost)) with { Room = StretchRoom.Picture };
}
