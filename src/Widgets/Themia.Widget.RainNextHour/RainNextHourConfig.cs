using Themia.Core.Sync;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>[S-5120] Rain in the next two hours at a place. A weather-family widget, like air quality.</summary>
public sealed class RainNextHourConfig : WeatherConfig, IInterests
{
    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public void AddInterests(InterestsBuilder interests)
    {
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
        var copy = new RainNextHourConfig();
        CopyTo(copy);
        return copy;
    }
}
