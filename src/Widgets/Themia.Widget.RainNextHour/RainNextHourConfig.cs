using Themia.Core.Sync;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// [S-5120] [S-14940] What every part of the rain family remembers: the place (a weather-family config, like air quality).
/// Each asks for the same nowcast of its place ([S-5121]); what it shows is its kind.
/// </summary>
public abstract class RainConfig : WeatherConfig, IInterests
{
    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public void AddInterests(InterestsBuilder interests)
    {
        ArgumentNullException.ThrowIfNull(interests);
        if (Interest(OnlineCadence.Rain(interests.Refresh)) is { } wants)
        {
            interests.Of<RainInterest>().Add(wants);
        }
    }

    /// <summary>[S-5121] What it asks for, or null without a place; the running app's cadence ([S-3780]).</summary>
    public RainInterest? Interest() => Interest(OnlineCadence.Rain(UpdateCadence.Current));

    /// <summary>[S-5121] <see cref="Interest()"/> at a given cadence.</summary>
    public RainInterest? Interest(TimeSpan interval)
        => WebInput.AreCoordinates(Latitude, Longitude) ? new RainInterest(Latitude!.Value, Longitude!.Value, interval) : null;

    public override WidgetConfig Clone()
    {
        var copy = Fresh();
        CopyTo(copy);
        return copy;
    }

    /// <summary>A new config of this kind.</summary>
    protected abstract RainConfig Fresh();
}

/// <summary>[S-5120] [S-14941] The rain's minute graph, a column per quarter hour: the kind <c>rainNextHour</c>.</summary>
public sealed class RainNextHourConfig : RainConfig
{
    protected override RainConfig Fresh() => new RainNextHourConfig();
}

/// <summary>[S-14942] The rain's sentence ("Rain in 15 min") on the text base: a line that sits where its card aligns it ([S-6581]).</summary>
public sealed class RainOutlookConfig : RainConfig, IContentAligned
{
    protected override RainConfig Fresh() => new RainOutlookConfig();
}

/// <summary>[S-14943] The rain the next two hours hold, a single metric.</summary>
public sealed class RainTotalConfig : RainConfig
{
    protected override RainConfig Fresh() => new RainTotalConfig();
}
