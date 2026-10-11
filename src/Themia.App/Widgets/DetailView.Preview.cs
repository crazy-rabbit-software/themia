#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): what a render machine cannot show by itself. <c>sample</c> draws a
/// laptop's battery on a desktop (fixture figures through the real <see cref="BatteryDetailRules"/>);
/// <c>focus</c> puts the keyboard ring on a list's second row.
/// </summary>
internal sealed partial class DetailView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "focus"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "sample" when _kind == Themia.Core.Model.WidgetKind.BatteryDetails:
                // Stop the real reads, which on a desktop would put the empty state back.
                _part.Detach();
                var power = new BatteryPower(true, 82, false, false, TimeSpan.FromMinutes(194));
                var pack = new BatteryPack(false, 57_000, 51_300, 42_000, -9_400, 12_380, 214, 31, "Li-polymer", "SMP", 1);
                Present(BatteryDetailRules.Sheet(Strings.Current, power, pack));
                break;

            case "focus":
                HandleKey(WidgetKey.Down);
                HandleKey(WidgetKey.Down);
                break;

            default:
                break;
        }

        return Task.CompletedTask;
    }
}
#endif
