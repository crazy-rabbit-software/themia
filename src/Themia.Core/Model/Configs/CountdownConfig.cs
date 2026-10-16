using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// [S-6030] How the Countdown draws its featured timer: the duration in words, a ring of the time
/// left, or split-flap cards. The rows under it are the same in every style.
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<CountdownStyle>))]
public enum CountdownStyle
{
    Text = 0,

    /// <summary>[S-6031] The time left as a ring, from when the timer was made to its target.</summary>
    Ring = 1,

    /// <summary>[S-6032] Days, hours and minutes on the Clock's split-flap cards.</summary>
    Flip = 2,
}

/// <summary>One dated target in a <see cref="CountdownConfig"/>. [C-10]</summary>
public sealed class CountdownTimer
{
    /// <summary>Stable id, so reordering and editing cannot swap two timers' settings.</summary>
    public required string Id { get; set; }

    public string Label { get; set; } = string.Empty;

    /// <summary>The instant being counted to, always stored in UTC. [C-53]</summary>
    public DateTimeOffset Target { get; set; }

    public CountdownFormat Format { get; set; } = CountdownFormat.Auto;

    /// <summary>
    /// [S-1448] When the timer was added, so a large tile can show how far along it is. The default
    /// (unset) means "unknown": no progress bar is drawn rather than one measured from year one.
    /// </summary>
    public DateTimeOffset Created { get; set; }

    public static CountdownTimer Create(string label, DateTimeOffset target, DateTimeOffset created = default) => new()
    {
        Id = Guid.NewGuid().ToString("n"),
        Label = label,
        Target = target,
        Created = created,
    };

    public CountdownTimer Clone() => new()
    {
        Id = Guid.NewGuid().ToString("n"),
        Label = Label,
        Target = Target,
        Format = Format,
        Created = Created,
    };
}

/// <summary>An ordered list of countdowns.</summary>
public sealed class CountdownConfig : WidgetConfig, IStyled
{
    private static readonly StyleOption[] CountdownStyles =
    [
        StyleOption.Of(CountdownStyle.Text, Str.Styles_Countdown_Text, 0xE8D2),
        StyleOption.Of(CountdownStyle.Ring, Str.Styles_Countdown_Ring, 0xEA3A),
        StyleOption.Of(CountdownStyle.Flip, Str.Styles_Clock_Flip, 0xE8A9),
    ];

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Countdown;

    /// <summary>[S-6030] The featured timer's look.</summary>
    public CountdownStyle Style { get; set; } = CountdownStyle.Text;

    IReadOnlyList<StyleOption> IStyled.Styles => CountdownStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (CountdownStyle)value;
    }

    public List<CountdownTimer> Timers { get; set; } = [];

    /// <summary>
    /// [C-20] One second only when a timer actually shows seconds; otherwise half a minute, which
    /// is enough for minute granularity and costs the process 120 fewer wakes a minute.
    /// </summary>
    [JsonIgnore]
    public TimeSpan TickInterval => Timers.Exists(t => t.Format == CountdownFormat.DaysHoursMinutesSeconds)
        ? TimeSpan.FromSeconds(1)
        : TimeSpan.FromSeconds(30);

    public override WidgetConfig Clone()
    {
        var copy = new CountdownConfig { Style = Style };
        foreach (var timer in Timers)
        {
            copy.Timers.Add(timer.Clone());
        }

        return copy;
    }
}
