using Themia.Core.Model;
using Themia.Core.Series;
using Themia.Sdk;

namespace Themia.Core.Tests;

/// <summary>A repeater's config as Core sees it, no module needed.</summary>
internal sealed class RepeaterFake : WidgetConfig, IRepeater
{
    public List<string> Members { get; set; } = [];

    public RepeatDirection Direction { get; set; }

    public int Spacing { get; set; }

    public int ColumnSpacing { get; set; }

    public SeriesType? RepeatBy { get; set; }

    public string? Values { get; set; }

    public int Start { get; set; }

    public int Step { get; set; } = 1;

    public List<string> Picked { get; set; } = [];

    public int Scroll { get; set; }

    public List<string> Unbound { get; set; } = [];

    public List<string> Chosen { get; set; } = [];

    public IReadOnlyList<SeriesType> Offered { get; set; } = [];

    public RepeaterShown? Shown { get; set; }

    public int DayWidth { get; set; }

    public int DayHeight { get; set; }

    public override WidgetConfig Clone() => new RepeaterFake
    {
        Members = [.. Members], Direction = Direction, Spacing = Spacing, ColumnSpacing = ColumnSpacing, RepeatBy = RepeatBy,
        Values = Values, Start = Start, Step = Step, Picked = [.. Picked], Scroll = Scroll, Unbound = [.. Unbound], Chosen = [.. Chosen],
        DayWidth = DayWidth, DayHeight = DayHeight,
    };
}

/// <summary>A drive atom's config.</summary>
internal sealed class DriveFake : WidgetConfig, IDriveChoice
{
    public string Drive { get; set; } = string.Empty;

    public override WidgetConfig Clone() => new DriveFake { Drive = Drive };
}

/// <summary>A day atom's config; <see cref="TakesDay"/> false for a reading of the present.</summary>
internal sealed class DayFake : WidgetConfig, IDayChoice
{
    public int Day { get; set; }

    public bool TakesDay { get; init; } = true;

    public override WidgetConfig Clone() => new DayFake { Day = Day, TakesDay = TakesDay };
}

/// <summary>A day atom about a place: two slots of different types.</summary>
internal sealed class PlaceDayFake : WidgetConfig, IPlaced, IDayChoice
{
    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string PlaceName { get; set; } = string.Empty;

    public bool UseDeviceLocation { get; set; }

    public bool HasLocation => Latitude is { } lat && Longitude is { } lon && double.IsFinite(lat) && double.IsFinite(lon);

    public int Day { get; set; }

    public bool TakesDay => true;

    public void ClearLocation()
    {
        Latitude = null;
        Longitude = null;
        PlaceName = string.Empty;
        UseDeviceLocation = false;
    }

    public WidgetConfig AtDevice(double? latitude, double? longitude, string name)
        => new PlaceDayFake { Latitude = latitude, Longitude = longitude, PlaceName = name, Day = Day };

    public override WidgetConfig Clone() => new PlaceDayFake
    {
        Latitude = Latitude, Longitude = Longitude, PlaceName = PlaceName, UseDeviceLocation = UseDeviceLocation, Day = Day,
    };
}

/// <summary>A widget with no slot at all (a header, a clock).</summary>
internal sealed class PlainFake : WidgetConfig
{
    public override WidgetConfig Clone() => new PlainFake();
}

/// <summary>A container that shows a set count of days.</summary>
internal sealed class CountedFake : WidgetConfig, IRepeater
{
    public List<string> Members { get; set; } = [];

    public RepeatDirection Direction { get; set; }

    public int Spacing { get; set; }

    public int ColumnSpacing { get; set; }

    public SeriesType? RepeatBy { get; set; }

    public string? Values { get; set; }

    public int Start { get; set; }

    public int Step { get; set; } = 1;

    public List<string> Picked { get; set; } = [];

    public int Scroll { get; set; }

    public List<string> Unbound { get; set; } = [];

    public List<string> Chosen { get; set; } = [];

    public IReadOnlyList<SeriesType> Offered { get; set; } = [];

    public RepeaterShown? Shown { get; set; }

    public int DayWidth { get; set; }

    public int DayHeight { get; set; }

    public int Days { get; set; }

    public int Count => Days;

    public override WidgetConfig Clone() => new CountedFake
    {
        Members = [.. Members], Direction = Direction, Spacing = Spacing, RepeatBy = RepeatBy, Start = Start, Scroll = Scroll,
        Unbound = [.. Unbound], Chosen = [.. Chosen], DayWidth = DayWidth, DayHeight = DayHeight, Days = Days,
    };
}

/// <summary>A container with a place of its own: a weather forecast as Core sees it.</summary>
internal sealed class ForecastFake : WeatherConfig, IRepeater
{
    public List<string> Members { get; set; } = [];

    public RepeatDirection Direction { get; set; }

    public int Spacing { get; set; }

    public int ColumnSpacing { get; set; }

    public SeriesType? RepeatBy { get; set; } = SeriesType.Day;

    public string? Values { get; set; }

    public int Start { get; set; }

    public int Step { get; set; } = 1;

    public List<string> Picked { get; set; } = [];

    public int Scroll { get; set; }

    public List<string> Unbound { get; set; } = [];

    public List<string> Chosen { get; set; } = [];

    public IReadOnlyList<SeriesType> Offered { get; set; } = [];

    public RepeaterShown? Shown { get; set; }

    public int DayWidth { get; set; }

    public int DayHeight { get; set; }

    public override WidgetConfig Clone()
    {
        var copy = new ForecastFake { Members = [.. Members], Direction = Direction, Start = Start, Scroll = Scroll, DayWidth = DayWidth, DayHeight = DayHeight };
        CopyTo(copy);
        return copy;
    }
}

/// <summary>A day part about a place: a weather atom as Core sees it.</summary>
internal sealed class PlacedDayFake : WeatherConfig, IDayChoice
{
    public int Day { get; set; }

    public bool TakesDay => true;

    public override WidgetConfig Clone()
    {
        var copy = new PlacedDayFake { Day = Day };
        CopyTo(copy);
        return copy;
    }
}
