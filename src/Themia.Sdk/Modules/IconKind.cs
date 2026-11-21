using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Sdk;

/// <summary>
/// [S-15003] One kind of an icon family, as data (owner, 2026-11-21: "there should be a shared widget backend for widgets that
/// show just an icon"): its id, its glyph (the add page's, and the face's own until its family says another: the sky now, play
/// or pause), its name and description on the add page, its search words and its config (a sealed type a kind: a config's
/// type is how it knows its id, [S-6902]).
/// </summary>
public sealed record IconKind(
    string Id,
    string Glyph,
    LocalText Label,
    LocalText Description,
    IReadOnlyList<string> Keywords,
    Func<WidgetConfig> Create)
{
    /// <summary>The config's type.</summary>
    public Type ConfigType { get; } = Create().GetType();

    /// <summary>
    /// [S-15004] What a click on the card opens and its card menu's line: its family's source (the place's forecast); null for
    /// a kind that opens nothing (it presses something, as a transport button, or does nothing, as the Icon widget).
    /// </summary>
    public IconOpen? Opens { get; init; }
}
