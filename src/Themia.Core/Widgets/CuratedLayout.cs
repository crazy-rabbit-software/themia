using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-16740] One tab of the widget gallery as the owner laid it (owner, 2026-11-23: "there are columns of which i can change
/// the size and then i can have sections in the columns that i can also name / give a title too. in a column the widgets are
/// automatically aligned and i can change their height and width and then they are packed together as tightly as possible in
/// direction of reading"): its columns left to right, each a fixed number of the grid's blocks wide ([S-17160]). Shipped as one file per tab
/// (<see cref="CuratedLayouts"/>), laid by <see cref="CuratedBento"/>.
/// </summary>
public sealed record CuratedLayout
{
    /// <summary>[S-16721] Whether each card stands under its small title (Quick settings: no, its sections' headings say it).</summary>
    public bool Titles { get; init; } = true;

    public IReadOnlyList<CuratedColumn> Columns { get; init; } = [];
}

/// <summary>
/// [S-16741] [S-17160] A column: its width in whole blocks (cells of the display's grid, about 10 DIPs: the same on every
/// display, never stretched; 0, missing, is repaired where read) and its sections, top down.
/// </summary>
public sealed record CuratedColumn
{
    public int Width { get; init; }

    public IReadOnlyList<CuratedSection> Sections { get; init; } = [];
}

/// <summary>[S-16742] A section of a column: its title (none: no heading) and its widgets in reading order.</summary>
public sealed record CuratedSection
{
    public string? Title { get; init; }

    public IReadOnlyList<CuratedWidget> Widgets { get; init; } = [];
}

/// <summary>
/// [S-16742] A widget of a section: what it places (<see cref="Pick"/>, a <see cref="Themia.Sdk.WidgetPick"/>'s key: the kind's
/// id, or the id and a preset's key, <c>quickToggle/wifi</c>), in which style (the style's name; none: the one it has new),
/// with which config fragment over its default (none: the default), and its card's size in whole cells of the display's
/// grid (about 10 DIPs each on every display).
/// </summary>
public sealed record CuratedWidget
{
    public string Pick { get; init; } = string.Empty;

    public string? Style { get; init; }

    public JsonObject? Config { get; init; }

    public int W { get; init; } = 1;

    public int H { get; init; } = 1;

    /// <summary>What it is, the same for two widgets that show the same thing: the pick and its style.</summary>
    [JsonIgnore]
    public string Identity => Style is { } style ? Pick + "@" + style : Pick;
}

/// <summary>[S-16742] Where a widget or section of a <see cref="CuratedLayout"/> stands: its column, its section, its place in it (-1: the section itself).</summary>
public readonly record struct CuratedSlot(int Column, int Section, int Widget = -1);

/// <summary>Source-generated JSON for the gallery's layout files. Native AOT has no reflection serializer.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CuratedLayout))]
[JsonSerializable(typeof(JsonObject))]
internal sealed partial class CuratedLayoutJson : JsonSerializerContext;
