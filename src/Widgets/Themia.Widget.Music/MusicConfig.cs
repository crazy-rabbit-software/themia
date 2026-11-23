using System.Text.Json;
using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Music;

namespace Themia.Core.Model;

/// <summary>[S-16500] Now playing's styles: the same track and controls, a few deliberate layouts.</summary>
[JsonConverter(typeof(StyleJsonConverter<MusicStyle>))]
public enum MusicStyle
{
    /// <summary>[S-16503] The cover at the left as tall as the texts, the progress line and the transport beside it.</summary>
    Side = 0,

    /// <summary>[S-16504] The cover on top, the texts, the progress line and the transport under it.</summary>
    Stacked = 1,

    /// <summary>[S-16505] The cover is the card's band; the texts and controls stand over a scrim at its foot.</summary>
    FullArt = 2,

    /// <summary>[S-16506] One line: the title and artist, the progress, the transport; no cover.</summary>
    Strip = 3,
}

/// <summary>[S-16501] An element of Now playing's face that can be hidden on its own (owner, 2026-11-22: "hide almost every element separately").</summary>
public enum MusicElement
{
    Cover = 0,
    Title = 1,
    Artist = 2,
    Album = 3,
    Elapsed = 4,
    Bar = 5,
    Length = 6,
    Previous = 7,
    PlayPause = 8,
    Next = 9,
}

/// <summary>[S-16501] The element set: every element, the canonical order, its repair and a toggle that keeps one.</summary>
public static class MusicElements
{
    /// <summary>Every element, in the order the face and the page show them; a new card shows them all.</summary>
    public static IReadOnlyList<MusicElement> All { get; } = Enum.GetValues<MusicElement>();

    /// <summary>
    /// [S-16501] A stored set in the canonical order with unknown values and repeats dropped (how many, for the load's line);
    /// an empty one is the whole set again.
    /// </summary>
    public static (List<MusicElement> Clean, int Dropped, bool Emptied) Clean(IEnumerable<MusicElement>? stored)
    {
        var known = new HashSet<MusicElement>();
        var dropped = 0;
        foreach (var element in stored ?? [])
        {
            if (!Enum.IsDefined(element) || !known.Add(element))
            {
                dropped++;
            }
        }

        var clean = All.Where(known.Contains).ToList();
        return clean.Count == 0 ? ([.. All], dropped, true) : (clean, dropped, false);
    }

    /// <summary>[S-16501] The set with <paramref name="element"/> turned on or off; the last one shown stays (a face of nothing is no face).</summary>
    public static List<MusicElement> Toggle(IReadOnlyCollection<MusicElement> shown, MusicElement element, bool on)
    {
        ArgumentNullException.ThrowIfNull(shown);
        var next = All.Where(e => e == element ? on : shown.Contains(e)).ToList();
        return next.Count == 0 ? [.. shown] : next;
    }
}

/// <summary>
/// [S-16501] The element set on the wire: each element's camelCase name. Reading never throws (a throw would refuse the whole
/// state file): an unknown name, or an item that is no string, reads as an undefined value the config's repair drops, loudly.
/// </summary>
public sealed class MusicElementsJsonConverter : JsonConverter<List<MusicElement>>
{
    private const MusicElement Unknown = (MusicElement)(-1);

    public override List<MusicElement> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var read = new List<MusicElement>();
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return read;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String && reader.GetString() is { Length: > 0 } wire && char.IsAsciiLetter(wire[0])
                && Enum.TryParse<MusicElement>(wire, ignoreCase: true, out var element) && Enum.IsDefined(element))
            {
                read.Add(element);
                continue;
            }

            reader.Skip();
            read.Add(Unknown);
        }

        return read;
    }

    public override void Write(Utf8JsonWriter writer, List<MusicElement> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStartArray();
        foreach (var element in value ?? [])
        {
            var name = element.ToString();
            writer.WriteStringValue(char.ToLowerInvariant(name[0]) + name[1..]);
        }

        writer.WriteEndArray();
    }
}

/// <summary>
/// [S-16500] Now playing: what Windows is playing, from its own media session, as one widget in a few deliberate styles
/// (<see cref="MusicStyle"/>), every element of its face shown or hidden on its own (<see cref="Shows"/>), and when it
/// hides ([S-1464]).
/// </summary>
public sealed class MusicConfig : WidgetConfig, IStyled, IHidesWhenIdle, IDrawsItems
{
    /// <summary>[S-16509] Its one item is the face's light: a fill, no text of the item layer's.</summary>
    bool IDrawsItems.DrawsItemText => false;

    /// <summary>[S-16500] The layout; a new card's the side cover.</summary>
    public MusicStyle Style { get; set; } = MusicStyle.Side;

    private static readonly StyleOption[] MusicStyles =
    [
        StyleOption.Of(MusicStyle.Side, MusicStr.Styles_Music_Side.Local(), 0xE93C),
        StyleOption.Of(MusicStyle.Stacked, MusicStr.Styles_Music_Stacked.Local(), 0xEA37),
        StyleOption.Of(MusicStyle.FullArt, MusicStr.Styles_Music_FullArt.Local(), 0xE740),
        StyleOption.Of(MusicStyle.Strip, MusicStr.Styles_Music_Strip.Local(), 0xE76F),
    ];

    IReadOnlyList<StyleOption> IStyled.Styles => MusicStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (MusicStyle)value;
    }

    /// <summary>[S-16501] The elements shown, in the canonical order; a new card shows every one.</summary>
    [JsonConverter(typeof(MusicElementsJsonConverter))]
    public List<MusicElement> Shows { get; set; } = [.. MusicElements.All];

    /// <summary>[S-1464] When the widget paints nothing at all; edit mode always shows it.</summary>
    public MusicHideWhenIdle HideWhenIdle { get; set; } = MusicHideWhenIdle.Never;

    /// <summary>[S-16501] Whether <paramref name="element"/> shows.</summary>
    public bool Has(MusicElement element) => Shows.Contains(element);

    /// <summary>An unknown hide-when-idle mode reads Never; the element set is cleaned, an empty one is every element again.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>();
        if (!Enum.IsDefined(HideWhenIdle))
        {
            HideWhenIdle = MusicHideWhenIdle.Never;
            notes.Add("Music widget had an unknown hide-when-idle mode");
        }

        var (clean, dropped, emptied) = MusicElements.Clean(Shows);
        if (dropped > 0)
        {
            notes.Add($"Music widget: {dropped} unknown or repeated element(s) dropped");
        }

        if (emptied)
        {
            notes.Add("Music widget showed no element; it shows every one again");
        }

        Shows = clean;
        return notes;
    }

    public override WidgetConfig Clone() => new MusicConfig
    {
        Style = Style,
        Shows = [.. Shows ?? []],
        HideWhenIdle = HideWhenIdle,
    };
}
