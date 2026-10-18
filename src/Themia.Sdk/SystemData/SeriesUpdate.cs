using Themia.Core.SystemData;

namespace Themia.App.Services.SystemData;

/// <summary>[S-2504] One read of a series: the reading to show, and the point it added to the history (null when none).</summary>
public readonly record struct SeriesUpdate(MeterReading Reading, SeriesPoint? Point);
