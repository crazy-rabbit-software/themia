using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Widget.AirQualityGraph;

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

/// <summary>[S-10400] An air graph: a place, its window (one to four days), its chart look ([S-6752]: the area by default), what it draws and the index's scale.</summary>
public sealed class AirQualityGraphConfig : HourGraphConfig
{
    public AirQualityGraphConfig()
        : base(ChartType.Area) => Hours = 48;

    public AirMeasure Measure { get; set; } = AirMeasure.Index;

    /// <summary>[S-5103] The index's scale: Auto by the place, as the air widget's.</summary>
    public AirQualityScale Scale { get; set; } = AirQualityScale.Auto;

    /// <summary>[S-10400] One to four days: CAMS forecasts the air that far.</summary>
    [JsonIgnore]
    public override IReadOnlyList<int> HourChoices => HourGraphRules.AirHourChoices;

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
        var copy = new AirQualityGraphConfig { Measure = Measure, Scale = Scale };
        CopyTo(copy);
        return copy;
    }
}
