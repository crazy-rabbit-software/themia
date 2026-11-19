using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Widget.Header;

namespace Themia.Widget.Header;

/// <summary>
/// [S-9600] What one Header widget remembers: its title, the glyph before it (a Segoe Fluent glyph from the
/// icon picker, or none) and an optional caption under it. Where it lies across its card is the widget's
/// alignment ([S-6581]). Every property is stored; copied in <see cref="Clone"/>.
/// <para>
/// [S-9604] [S-12802] It may name the place of the widgets it heads ("Weather · Zürich"): it holds the place every
/// widget about one shares (<see cref="IPlaced"/>, so a group's page sets it once on the header and its mates,
/// [S-11753]) and shows its city after the title while it holds one; its own page has no place line (owner,
/// 2026-11-19). The widgets under it name no place of their own ([S-12801]). It never waits for a place: without one
/// it shows the title alone.
/// </para>
/// </summary>
public sealed class HeaderConfig : WidgetConfig, IContentAligned, IPlaced
{
    /// <summary>The title; empty draws the word "Title" so a new one is never a blank strip.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>[S-9602] The glyph before the title, or empty for none.</summary>
    public string Glyph { get; set; } = string.Empty;

    /// <summary>[S-9601] A line under the title (a date, a note), or empty for none: the strip is one line taller with one.</summary>
    public string Caption { get; set; } = string.Empty;

    /// <summary>[S-9604] Degrees north of the place it names, or null while none is set.</summary>
    public double? Latitude { get; set; }

    /// <summary>[S-9604] Degrees east of the place it names, or null while none is set.</summary>
    public double? Longitude { get; set; }

    /// <summary>[S-9604] The name the place resolved to ("Zürich, Zurich, Switzerland"); its city is what the header shows.</summary>
    public string PlaceName { get; set; } = string.Empty;

    /// <summary>[S-2850] The place is wherever Windows says the device is (the host hands the view a copy at it).</summary>
    public bool UseDeviceLocation { get; set; }

    /// <summary>[S-9601] Whether a caption is drawn: the sizing's second line.</summary>
    public bool HasCaption => !string.IsNullOrWhiteSpace(Caption);

    /// <summary>[S-11752] Both coordinates set and finite.</summary>
    public bool HasLocation => Latitude is { } lat && Longitude is { } lon && double.IsFinite(lat) && double.IsFinite(lon);

    /// <summary>
    /// [S-9604] The city the title is followed by: the first part of the place's name while the header holds one (the
    /// device's "My location" while it uses that); null otherwise.
    /// </summary>
    public string? PlaceLabel => (HasLocation || UseDeviceLocation) ? LineRules.About(PlaceName.Split(',', 2)[0]) : null;

    /// <summary>[S-9604] A header never waits for setup: without a place it is its title.</summary>
    bool ISetupRule.NeedsSetup(FeatureSet signedIn) => false;

    /// <summary>[S-2848] Forget the place: the header shows its title alone.</summary>
    public void ClearLocation()
    {
        Latitude = null;
        Longitude = null;
        PlaceName = string.Empty;
        UseDeviceLocation = false;
    }

    /// <summary>[S-2853] A copy at the device's position, named <paramref name="name"/> ("My location").</summary>
    public WidgetConfig AtDevice(double? latitude, double? longitude, string name)
    {
        var copy = (HeaderConfig)Clone();
        copy.Latitude = latitude;
        copy.Longitude = longitude;
        copy.PlaceName = latitude is null ? string.Empty : name;
        return copy;
    }

    public override WidgetConfig Clone() => new HeaderConfig
    {
        Text = Text,
        Glyph = Glyph,
        Caption = Caption,
        Latitude = Latitude,
        Longitude = Longitude,
        PlaceName = PlaceName,
        UseDeviceLocation = UseDeviceLocation,
    };

    /// <summary>
    /// [S-9600] Hand-edited or corrupt text is trimmed to <see cref="HeaderLayout.MostText"/> characters and a
    /// glyph that is not one character of the private-use area (where Segoe Fluent's glyphs are) is dropped;
    /// [S-9604] a place with one coordinate, or one out of range, is cleared.
    /// </summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();
        var text = HeaderLayout.Clean(Text);
        if (text != Text)
        {
            Text = text;
            repairs.Add("header: title trimmed");
        }

        var caption = HeaderLayout.Clean(Caption);
        if (caption != Caption)
        {
            Caption = caption;
            repairs.Add("header: caption trimmed");
        }

        var place = HeaderLayout.Clean(PlaceName);
        if (place != PlaceName)
        {
            PlaceName = place;
            repairs.Add("header: place name trimmed");
        }

        if (!HeaderLayout.IsGlyph(Glyph))
        {
            Glyph = string.Empty;
            repairs.Add("header: glyph dropped");
        }

        var half = (Latitude is null) != (Longitude is null);
        var outside = Latitude is { } lat && (!double.IsFinite(lat) || Math.Abs(lat) > 90)
            || Longitude is { } lon && (!double.IsFinite(lon) || Math.Abs(lon) > 180);
        if (half || outside)
        {
            Latitude = null;
            Longitude = null;
            PlaceName = string.Empty;
            repairs.Add("header: place cleared");
        }

        return repairs;
    }
}
