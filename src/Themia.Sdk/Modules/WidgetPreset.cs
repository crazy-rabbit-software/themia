using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Sdk;

/// <summary>
/// [S-16680] One way a kind is offered as if it were a widget of its own (owner, 2026-11-23: "they should actually be
/// merged into one widget and not be multiple ones. in the gallery and everywhere else they should still appear as if
/// they were multiple widgets though. but in the backend it is the same one."): its own name, caption, glyph, search
/// words and place on the add page, and what it sets on a new config of the kind (<see cref="Apply"/>). The add page, its
/// search, the gallery, the ready-made groups and the first-run seed list one entry per preset in place of the kind's one
/// (<see cref="WidgetPick"/>); placing one makes the kind with the preset applied; a placed widget the preset
/// <see cref="Holds"/> is named as it.
/// </summary>
public sealed record WidgetPreset
{
    /// <summary>Its key among the kind's presets, the same in every language: a pick's second half (<c>quickToggle/wifi</c>).</summary>
    public required string Key { get; init; }

    /// <summary>Its name, as a kind's <see cref="WidgetDescriptor.Label"/>.</summary>
    public required LocalText Label { get; init; }

    /// <summary>Its caption, as a kind's <see cref="WidgetDescriptor.Description"/>.</summary>
    public required LocalText Description { get; init; }

    /// <summary>Its glyph: the add page's tile, the gallery's words, the selection's entry.</summary>
    public required string Glyph { get; init; }

    /// <summary>[S-943] Its curated search words, English only, as a kind's.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>[S-6907] Its home on the add page and its place there, as a kind's.</summary>
    public required AddPlacement Add { get; init; }

    /// <summary>What the preset sets on a new config of its kind (its default, changed in place).</summary>
    public required Action<WidgetConfig> Apply { get; init; }

    /// <summary>Whether a config of the kind is this preset: a placed widget is named as the preset that holds it.</summary>
    public required Func<WidgetConfig, bool> Holds { get; init; }
}

/// <summary>
/// [S-16680] [S-16682] What the add page, the gallery, a ready-made group or the first-run seed offers and places: a kind,
/// or one of its presets (<see cref="WidgetDescriptor.Presets"/>). Its <see cref="Key"/> is the kind's id, or the id and
/// the preset's key with a slash (<c>quickToggle/wifi</c>): the one name the data uses for either. Every word, glyph and
/// new config the offer shows or makes is read here, so no caller asks the kind and its presets apart.
/// </summary>
public readonly record struct WidgetPick(WidgetId Kind, string? Preset = null)
{
    /// <summary>Between a kind's id and its preset's key in <see cref="Key"/>.</summary>
    public const char Separator = '/';

    /// <summary>A kind alone is a pick of the kind.</summary>
    public static implicit operator WidgetPick(WidgetId kind) => new(kind);

    /// <summary>The kind's id, or the id and the preset's key: the same in every language.</summary>
    public string Key => Preset is null ? Kind.Value : Kind.Value + Separator + Preset;

    public override string ToString() => Key;

    /// <summary>The kind's descriptor.</summary>
    public WidgetDescriptor Descriptor => ModuleCatalog.Of(Kind);

    /// <summary>The preset picked; null for the kind itself. A preset the kind does not have is a caller's bug, thrown.</summary>
    public WidgetPreset? Entry
    {
        get
        {
            if (Preset is not { } key)
            {
                return null;
            }

            return Descriptor.Presets.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.Ordinal))
                ?? throw new KeyNotFoundException($"the widget '{Kind}' has no preset '{key}'");
        }
    }

    /// <summary>Its name: the preset's, else the kind's.</summary>
    public LocalText Label => Entry?.Label ?? Descriptor.Label;

    /// <summary>Its name in the current language.</summary>
    public string Name => Label.Text();

    /// <summary>[S-11709] What its tile on the add page is called: the preset's name, else the kind's tile name.</summary>
    public string TileName => Entry is { } preset ? preset.Label.Text() : Descriptor.TileName;

    /// <summary>Its caption in the current language.</summary>
    public string Describe() => Describe(Strings.Current);

    /// <summary>Its caption in <paramref name="locale"/>: the preset's, else the kind's with its facts.</summary>
    public string Describe(Locale locale) => Entry is { } preset ? preset.Description.Text(locale) : Descriptor.Describe(locale);

    /// <summary>Its glyph: the preset's, else the kind's.</summary>
    public string Glyph => Entry?.Glyph ?? Descriptor.Glyph;

    /// <summary>[S-943] Its search words: the preset's, else the kind's.</summary>
    public IReadOnlyList<string> Keywords => Entry?.Keywords ?? Descriptor.Keywords;

    /// <summary>[S-6907] Its place on the add page: the preset's, else the kind's.</summary>
    public AddPlacement Add => Entry?.Add ?? Descriptor.Add;

    /// <summary>[S-16683] A new config of it: the kind's default, the preset applied.</summary>
    public WidgetConfig CreateConfig()
    {
        var config = Descriptor.CreateDefault();
        Entry?.Apply(config);
        return config;
    }

    /// <summary>[S-2438] Its sizing: the kind's, of its new config.</summary>
    public WidgetSizing Sizing => Descriptor.SizingOf(CreateConfig());

    /// <summary>[S-10] Where a new one lands, in DIPs: its new config's Natural.</summary>
    public SizeDip Natural => Sizing.Natural;

    /// <summary>[S-16682] A pick's key in its two halves, read without the catalog: the kind's id and the preset's key (null: none).</summary>
    public static (string Kind, string? Preset) Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var at = text.IndexOf(Separator, StringComparison.Ordinal);
        return at < 0 ? (text, null) : (text[..at], text[(at + 1)..]);
    }

    /// <summary>
    /// [S-16682] A pick named by data (a ready-made group's widget, a first-run seed, a command line): an installed kind's id,
    /// or that id, a slash and one of its presets' keys. False for anything else.
    /// </summary>
    public static bool TryParse(string? text, out WidgetPick pick)
    {
        pick = default;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var (kindText, preset) = Split(text);
        if (!WidgetId.TryParse(kindText, out var kind) || !ModuleCatalog.TryGet(kind, out var descriptor)
            || (preset is not null && !descriptor.Presets.Any(p => string.Equals(p.Key, preset, StringComparison.Ordinal))))
        {
            return false;
        }

        pick = new WidgetPick(kind, preset);
        return true;
    }

    /// <summary>
    /// [S-16684] What a placed widget is offered as: the first preset of its kind that holds its config, else the kind. A
    /// widget is named and drawn by it on the selection's entry and page, and in the placing hint.
    /// </summary>
    public static WidgetPick Of(WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var kind = ModuleCatalog.IdOf(config);
        var preset = ModuleCatalog.Of(kind).Presets.FirstOrDefault(p => p.Holds(config));
        return new WidgetPick(kind, preset?.Key);
    }
}
