using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-4440] The display brightness, one slider.</summary>
public sealed class BrightnessConfig : WidgetConfig
{
    /// <summary>[S-4442] Every display at once (the default), or the one the widget is on.</summary>
    public BrightnessScreens Screens { get; set; } = BrightnessScreens.All;

    public override WidgetConfig Clone() => new BrightnessConfig { Screens = Screens };

    /// <summary>An unknown number in a hand-edited file means the default.</summary>
    public override IReadOnlyList<string> Repair()
    {
        if (Enum.IsDefined(Screens))
        {
            return [];
        }

        var was = Screens;
        Screens = BrightnessScreens.All;
        return [$"brightness screens '{(int)was}' is unknown; setting every display"];
    }
}
