using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>
/// [S-5300] The Date widget: the weekday and the date, no time. Its settings are the Clock's that
/// make sense without a time ([S-5303]): where the column sits, and whether the year is shown.
/// </summary>
public sealed class DateConfig : WidgetConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Date;

    /// <summary>[S-5303] Left, centre or right, as the Clock's [S-1484].</summary>
    public ClockAlign Align { get; set; } = ClockAlign.Center;

    /// <summary>[S-5303] The year after the month. Off by default: the year is rarely the news.</summary>
    public bool ShowYear { get; set; }

    /// <summary>[S-5304] A minute: the date turns over at midnight, and a minute late is the cheapest correct cadence on the shared ticker.</summary>
    [JsonIgnore]
    public TimeSpan TickInterval => TimeSpan.FromMinutes(1);

    public override WidgetConfig Clone() => new DateConfig { Align = Align, ShowYear = ShowYear };

    /// <summary>[S-5303] An alignment outside its set (a hand edit) is centred, and said.</summary>
    public override IReadOnlyList<string> Repair()
    {
        if (Enum.IsDefined(Align))
        {
            return [];
        }

        var repair = $"date alignment {(int)Align} unknown; centred";
        Align = ClockAlign.Center;
        return [repair];
    }
}
