namespace Themia.Core.Series;

/// <summary>
/// [S-15510] A kind of value a repeater can run over: what a slot takes and a source gives. A value is
/// carried as a token (<see cref="SeriesTokens"/>), the same text whatever reads it, so the repeater never
/// knows what a day, a drive or a place is.
/// </summary>
public enum SeriesType
{
    /// <summary>[S-15513] A day from today: <c>0</c> is today, <c>1</c> tomorrow.</summary>
    Day = 0,

    /// <summary>[S-15513] A drive by its root (<c>C:\</c>).</summary>
    Drive = 1,

    /// <summary>[S-15513] A place on Earth: its latitude, longitude and name (<see cref="SeriesTokens.Place"/>).</summary>
    Place = 2,
}
