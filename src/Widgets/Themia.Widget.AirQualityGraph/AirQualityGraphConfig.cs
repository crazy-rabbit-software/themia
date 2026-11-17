using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Widget.AirQualityGraph;

/// <summary>[S-10401] The air graph's looks: the same hours, the curve with its wash or a column per hour.</summary>
[JsonConverter(typeof(StyleJsonConverter<AirQualityGraphStyle>))]
public enum AirQualityGraphStyle
{
    Area = 0,
    Bars = 1,
}

/// <summary>[S-10402] What the air graph draws: the index the air widget reads ([S-5103]) or one pollutant.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AirMeasure>))]
public enum AirMeasure
{
    Index = 0,
    Pm25 = 1,
    Pm10 = 2,
    Ozone = 3,
    NitrogenDioxide = 4,
}

/// <summary>[S-10400] An air graph: a place, its window (one to four days), its look, what it draws and the index's scale.</summary>
public sealed class AirQualityGraphConfig : HourGraphConfig, IStyled
{
    private static readonly StyleOption[] Looks =
    [
        StyleOption.Of(AirQualityGraphStyle.Area, Str.WeatherGraph_Style_Area.Local(), 0xE9D2),
        StyleOption.Of(AirQualityGraphStyle.Bars, Str.WeatherGraph_Style_Bars.Local(), 0xE9D9),
    ];

    public AirQualityGraphConfig() => Hours = 48;

    public AirQualityGraphStyle Style { get; set; } = AirQualityGraphStyle.Area;

    public AirMeasure Measure { get; set; } = AirMeasure.Index;

    /// <summary>[S-5103] The index's scale: Auto by the place, as the air widget's.</summary>
    public AirQualityScale Scale { get; set; } = AirQualityScale.Auto;

    /// <summary>[S-10400] One to four days: CAMS forecasts the air that far.</summary>
    [JsonIgnore]
    public override IReadOnlyList<int> HourChoices => HourGraphRules.AirHourChoices;

    IReadOnlyList<StyleOption> IStyled.Styles => Looks;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (AirQualityGraphStyle)value;
    }

    /// <summary>[S-10400] It asks for the air's hourly answer, not the weather's.</summary>
    public override void AddInterests(InterestsBuilder interests)
    {
        ArgumentNullException.ThrowIfNull(interests);
        if (AirInterest(OnlineCadence.Air(interests.Refresh)) is { } wants)
        {
            interests.Of<AirHoursInterest>().Add(wants);
        }
    }

    /// <summary>[S-12702] Its want at the running app's cadence (the air's, never faster than hourly), or null without a place.</summary>
    public AirHoursInterest? AirInterest() => AirInterest(OnlineCadence.Air(UpdateCadence.Current));

    public AirHoursInterest? AirInterest(TimeSpan interval)
        => WebInput.AreCoordinates(Latitude, Longitude) ? new AirHoursInterest(Latitude!.Value, Longitude!.Value, interval) : null;

    /// <summary>A measure or a scale that is none is put back to its default, and says so; the window as every graph's.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = base.Repair().ToList();
        if (!Enum.IsDefined(Measure))
        {
            repairs.Add($"air graph: measure {(int)Measure} is not one; Index");
            Measure = AirMeasure.Index;
        }

        if (!Enum.IsDefined(Scale))
        {
            repairs.Add($"air graph: index {(int)Scale} is not one; Auto");
            Scale = AirQualityScale.Auto;
        }

        return repairs;
    }

    public override WidgetConfig Clone()
    {
        var copy = new AirQualityGraphConfig { Style = Style, Measure = Measure, Scale = Scale };
        CopyTo(copy);
        return copy;
    }
}
