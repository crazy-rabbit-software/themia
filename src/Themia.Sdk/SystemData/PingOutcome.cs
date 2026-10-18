namespace Themia.App.Services;

/// <summary>
/// The result of one probe: what to show, plus the detail a log line would want.
/// <para>
/// Two fields rather than one because the caller logs on a <i>transition</i>, not on every probe. A
/// host that has been down for an hour is one log line, not eighteen hundred — but when that line is
/// finally written it still has to say which failure it was. <see cref="Detail"/> is null when there
/// is nothing to say, so "did this probe change anything worth writing down" is a null check rather
/// than a string comparison against the display text.
/// </para>
/// </summary>
/// <param name="Reading">What the tile shows.</param>
/// <param name="Detail">Why, in log terms, or null when the probe succeeded.</param>
public readonly record struct PingOutcome(MeterReading Reading, string? Detail);
