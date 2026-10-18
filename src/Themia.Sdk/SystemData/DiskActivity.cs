using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>[S-1162] One collection of the disk counters: rates per drive letter.</summary>
/// <param name="Rates">Read and write bytes per second, keyed by <c>\LogicalDisk</c> instance (<c>C:</c>).</param>
public readonly record struct DiskActivity(IReadOnlyDictionary<string, DiskRates> Rates);
