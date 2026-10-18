using Themia.App.Services.Web;
using Themia.Core.Localization;
using Themia.Sdk.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-5160] [S-7042] What the online widgets share: their one answer as the kit's one stale rule
/// reads it. While the answer on screen is one the last attempt could not improve on, the header's
/// problem glyph (<c>KitHeader</c>) says so ("No network · updated 10:42"); the card menu's Refresh
/// line says how old it is. The age itself is never drawn in the header.
/// </summary>
public static class OnlineParts
{
    /// <summary>How the answer last went, as a kit source's outcome (one source, no owner).</summary>
    public static KitOutcome Outcome<T>(Fetched<T> held)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(held);
        return new KitOutcome(null, held.Status, null, held.Value is null ? null : held.At);
    }

    /// <summary>The header's problem lines: none while the answer on screen is the last attempt's, or nothing is on screen.</summary>
    public static IReadOnlyList<string> Problems<T>(Fetched<T>? held)
        where T : class
        => held is { Value: not null } ? KitFreshness.Lines(Strings.Current, [Outcome(held)], DateTimeOffset.Now) : [];

    /// <summary>The card menu's Refresh line: "Refresh · updated 4 min ago", or "Refresh" while nothing is held.</summary>
    public static string RefreshLabel<T>(Fetched<T>? held)
        where T : class
        => KitFreshness.RefreshLabel(Strings.Current, held is null ? [] : [Outcome(held)], DateTimeOffset.Now);
}
