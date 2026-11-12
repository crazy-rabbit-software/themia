using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Widget.AirFacts;

/// <summary>
/// [S-11135] What one air facts card remembers: its place (the weather family's, [S-2840]) and its rows,
/// which and in what order. It asks for the same air answer as the index (<c>airQuality</c>, [S-5101]).
/// </summary>
public sealed class AirFactsConfig : WeatherConfig, IInterests
{
    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public void AddInterests(InterestsBuilder interests)
    {
        if (AirInterests.For(this, OnlineCadence.Air(interests.Refresh)) is { } wants)
        {
            interests.Of<AirInterest>().Add(wants);
        }
    }

    /// <summary>[S-11135] The rows it shows, in order: the card's height follows the count ([S-11136]).</summary>
    public List<AirFactKind> Facts { get; set; } = [.. AirFactRules.Defaults];

    public override IReadOnlyList<string> Repair()
    {
        var (facts, changed) = SkyFacts.Repair<AirFactKind>(Facts, AirFactRules.All, AirFactRules.Defaults);
        Facts = facts;
        return changed ? ["air facts: the pick was empty or held a row twice or one it does not have; repaired"] : [];
    }

    public override WidgetConfig Clone()
    {
        var copy = new AirFactsConfig { Facts = [.. Facts] };
        CopyTo(copy);
        return copy;
    }
}
