using Themia.Core.Widgets;

namespace Themia.App.Services.Brightness;

/// <summary>What the brightness widgets show: whether the displays have been looked at, and those with a brightness.</summary>
public sealed record BrightnessSnapshot(bool Ready, IReadOnlyList<BrightnessDisplay> Displays)
{
    public static BrightnessSnapshot Pending { get; } = new(false, []);
}
