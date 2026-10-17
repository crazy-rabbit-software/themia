using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-6402] How the pictures are laid out on the card: the gallery's styles, what was its Layout
/// setting. A folder's choice: one picture file and the photo of the day are drawn whole whatever
/// the style ([S-762]).
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<GalleryStyle>))]
public enum GalleryStyle
{
    /// <summary>One picture at a time, filling the card, advancing on a timer.</summary>
    Slideshow = 0,

    /// <summary>As many tiles as the card has room for.</summary>
    Grid = 1,

    /// <summary>[S-764] Prints dropped on a table: random place, size and tilt per picture.</summary>
    Scatter = 2,

    /// <summary>[S-765] The card cut into rectangles of varied size, one picture each, no gaps.</summary>
    Mosaic = 3,
}

/// <summary>[S-760] Where the pictures come from. A closed set.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GallerySource>))]
public enum GallerySource
{
    /// <summary>The pictures in a folder (and, optionally, the folders below it).</summary>
    Folder = 0,

    /// <summary>One picture file.</summary>
    File = 1,

    /// <summary>Bing's photo of the day. Needs no setup.</summary>
    Bing = 2,
}

/// <summary>What a picture does with a frame that is not its shape.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GalleryFit>))]
public enum GalleryFit
{
    /// <summary>Fill the frame and crop the overflow. No letterboxing, no whole picture.</summary>
    Fill = 0,

    /// <summary>Show the whole picture and leave the frame's spare space empty.</summary>
    Fit = 1,
}

/// <summary>[S-6074] The gallery's rows that only some sources, styles or states use.</summary>
public enum GallerySetting
{
    Folder,
    File,
    Recursive,
    SlideSeconds,
    Fit,
    Rotation,
    Outline,
    OutlineColor,
    OutlineWidth,
    Count,
    Shuffle,
    OpenFolder,
}

/// <summary>
/// Pictures from a folder on this PC, one picture file, or Bing's photo of the day.
/// <para>
/// An empty <see cref="Path"/> means <b>no folder chosen</b>, and the widget says so. It
/// deliberately does not fall back to the Pictures library the way the folder widget falls back to
/// the profile folder: a file list showing an unasked-for folder is merely surprising, whereas a
/// photo frame that starts showing pictures nobody pointed it at, on a surface that sits on the
/// desktop in front of whoever walks past, is a different kind of mistake.
/// </para>
/// </summary>
public sealed class GalleryConfig : WidgetConfig, IStyled, IShownSettings<GallerySetting>, ISetupRule
{
    /// <summary>[S-6906] It waits for setup while this holds.</summary>
    public bool NeedsSetup(AccountFeatures signedIn) => Source switch
    {
        GallerySource.Folder => string.IsNullOrWhiteSpace(Path),
        GallerySource.File => string.IsNullOrWhiteSpace(FilePath),
        _ => false,
    };

    /// <summary>
    /// [S-1620] The fewest seconds a slide can be held: one, as in the original (its S-09/S-18).
    /// Below a second it is a flicker, not a slideshow.
    /// </summary>
    public const int MinSlideSeconds = 1;

    /// <summary>An hour. Past it the timer is indistinguishable from a picture that never changes.</summary>
    public const int MaxSlideSeconds = 3600;

    /// <summary>
    /// The most pictures one widget will hold decoded at once, and so the ceiling on the setting.
    /// A folder can hold fifty thousand photographs; this is what stops the widget from trying.
    /// </summary>
    public const int MaxImageCount = 60;

    /// <summary>
    /// [S-772] How often the source is read again: hourly, for every gallery. A folder changes when
    /// something writes to it, which no timer can know, so this is a slow backstop rather than the
    /// refresh, and Bing's photo changes once a day. Not a setting ([S-3792]): "Refresh images" on
    /// the widget's menu is the way to see a change now.
    /// </summary>
    public static readonly TimeSpan RescanInterval = TimeSpan.FromHours(1);

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Gallery;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("gallery"),
        Kind = WidgetKind.Gallery,
        ConfigType = typeof(GalleryConfig),
        CreateDefault = () => new GalleryConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["photos", "pictures", "images", "slideshow", "wallpaper", "bing"],
        LabelId = Str.Widget_Gallery_Label,
        DescriptionId = Str.WidgetsA_Catalog_Gallery_Description,
        Glyph = Glyphs.Gallery,
        DefaultWidth = 320,
        DefaultHeight = 240,
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Media, 1, 0),
    };

    /// <summary>[S-760] Where the pictures come from.</summary>
    public GallerySource Source { get; set; } = GallerySource.Folder;

    /// <summary>The folder to show. Empty means none has been chosen yet.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>[S-761] The one picture the <see cref="GallerySource.File"/> source shows. Empty means none chosen.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Include pictures in folders below this one.</summary>
    public bool Recursive { get; set; }

    /// <summary>[S-6402] Slideshow, grid, scatter or mosaic: the gallery's style.</summary>
    public GalleryStyle Style { get; set; } = GalleryStyle.Slideshow;

    private static readonly StyleOption[] GalleryStyles =
    [
        StyleOption.Of(GalleryStyle.Slideshow, Str.Gallery_ModeSlideshow, 0xE786),
        StyleOption.Of(GalleryStyle.Grid, Str.Gallery_ModeGrid, 0xF0E2),
        StyleOption.Of(GalleryStyle.Scatter, Str.Gallery_ModeScatter, 0xE7AA),
        StyleOption.Of(GalleryStyle.Mosaic, Str.Gallery_ModeMosaic, 0xECA5),
    ];

    IReadOnlyList<StyleOption> IStyled.Styles => GalleryStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (GalleryStyle)value;
    }

    public GalleryFit Fit { get; set; } = GalleryFit.Fill;

    /// <summary>
    /// Take the pictures in a random order rather than in name order. Re-rolled on each rescan, so a
    /// folder bigger than <see cref="MaxImages"/> eventually shows all of it.
    /// </summary>
    public bool Shuffle { get; set; } = true;

    /// <summary>How long each slide is held, in seconds.</summary>
    public int SlideshowSeconds { get; set; } = 10;

    /// <summary>How many pictures are taken from the folder.</summary>
    public int MaxImages { get; set; } = 24;

    /// <summary>[S-774] One picture turned by this many degrees (any whole number; read through <see cref="EffectiveRotation"/>).</summary>
    public int Rotation { get; set; }

    /// <summary>[S-775] One picture outlined along its own shape, like a sticker.</summary>
    public bool Outline { get; set; }

    /// <summary>[S-775] The outline's colour, <c>#RRGGBB</c>; one that does not parse is white.</summary>
    public string OutlineColor { get; set; } = StickerRules.DefaultOutlineColor;

    /// <summary>[S-775] The outline's width in DIPs, <see cref="StickerRules.MinOutline"/>..<see cref="StickerRules.MaxOutline"/>.</summary>
    public int OutlineWidth { get; set; } = StickerRules.DefaultOutline;

    /// <summary>[S-774] The turn in (−180, 180].</summary>
    [JsonIgnore]
    public int EffectiveRotation => StickerRules.NormalizeDegrees(Rotation);

    [JsonIgnore]
    public string EffectiveOutlineColor => HexColor.Normalise(OutlineColor) is { Length: > 0 } colour ? colour : StickerRules.DefaultOutlineColor;

    [JsonIgnore]
    public int EffectiveOutlineWidth => Math.Clamp(OutlineWidth, StickerRules.MinOutline, StickerRules.MaxOutline);

    /// <summary>
    /// [S-774] A single picture file turned or outlined: drawn whole (never cropped), turned and
    /// outlined. The photo of the day and folders are not stickers.
    /// </summary>
    [JsonIgnore]
    public bool IsSticker => Source == GallerySource.File && (EffectiveRotation != 0 || Outline);

    /// <summary>
    /// [S-6074] The rows the page offers, by source and style: a folder's own rows (the folder, its
    /// subfolders, how many, the shuffle, the way out to Explorer), the slide time only for the
    /// slideshow, a picture file's rows (the file, its turn and outline, the outline's colour and
    /// width with the outline on), and the fit wherever a picture can be letterboxed: not on a
    /// scatter or a mosaic, which always crop ([S-15]), nor on an outlined sticker, always whole.
    /// The photo of the day has neither: one picture, filled or fitted. A rule never reads a
    /// slider's value, so dragging one never rebuilds the page under the pointer.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<GallerySetting> ShownSettings
    {
        get
        {
            var shown = new List<GallerySetting>();
            switch (Source)
            {
                case GallerySource.Folder:
                    shown.Add(GallerySetting.Folder);
                    shown.Add(GallerySetting.Recursive);
                    if (Style == GalleryStyle.Slideshow)
                    {
                        shown.Add(GallerySetting.SlideSeconds);
                    }

                    if (Style is GalleryStyle.Slideshow or GalleryStyle.Grid)
                    {
                        shown.Add(GallerySetting.Fit);
                    }

                    shown.Add(GallerySetting.Count);
                    shown.Add(GallerySetting.Shuffle);
                    shown.Add(GallerySetting.OpenFolder);
                    break;

                case GallerySource.File:
                    shown.Add(GallerySetting.File);
                    if (!Outline)
                    {
                        shown.Add(GallerySetting.Fit);
                    }

                    shown.Add(GallerySetting.Rotation);
                    shown.Add(GallerySetting.Outline);
                    if (Outline)
                    {
                        shown.Add(GallerySetting.OutlineColor);
                        shown.Add(GallerySetting.OutlineWidth);
                    }

                    break;

                default:
                    shown.Add(GallerySetting.Fit);
                    break;
            }

            return shown;
        }
    }

    [JsonIgnore]
    public int EffectiveMaxImages => Math.Clamp(MaxImages, 1, MaxImageCount);

    [JsonIgnore]
    public int EffectiveSlideshowSeconds => Math.Clamp(SlideshowSeconds, MinSlideSeconds, MaxSlideSeconds);

    /// <summary>How long one slide is held. The slideshow owns its own timer; this is its period.</summary>
    [JsonIgnore]
    public TimeSpan SlideInterval => TimeSpan.FromSeconds(EffectiveSlideshowSeconds);

    /// <summary>The rescan cadence: <see cref="RescanInterval"/>, the same for every gallery.</summary>
    [JsonIgnore]
    public TimeSpan TickInterval => RescanInterval;

    /// <summary>[S-762] File and Bing show one picture full-bleed; the layouts are a folder's.</summary>
    [JsonIgnore]
    public bool IsSingleSource => Source != GallerySource.Folder;

    public override WidgetConfig Clone() => new GalleryConfig
    {
        Source = Source,
        Path = Path,
        FilePath = FilePath,
        Recursive = Recursive,
        Style = Style,
        Fit = Fit,
        Shuffle = Shuffle,
        SlideshowSeconds = SlideshowSeconds,
        MaxImages = MaxImages,
        Rotation = Rotation,
        Outline = Outline,
        OutlineColor = OutlineColor,
        OutlineWidth = OutlineWidth,
    };
}
