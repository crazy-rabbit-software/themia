using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.RainNextHour;

[assembly: ThemiaWidget("rainNextHour", typeof(RainNextHourModule))]

namespace Themia.Widget.RainNextHour;

/// <summary>[S-5120] Rain in the next two hours at a place: one sentence, then the intensity and chance, then a bar per quarter hour. SPEC.md beside this file.</summary>
public sealed class RainNextHourModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("rainNextHour"),
        ConfigType = typeof(RainNextHourConfig),
        CreateDefault = () => new RainNextHourConfig(),
        Json = RainNextHourJsonContext.Default,
        Keywords = ["rain", "precipitation", "shower", "umbrella", "radar", "nowcast", "drizzle", "weather"],
        Label = RainNextHourStr.Online_Rain_Label.Local(),
        Description = RainNextHourStr.Online_Rain_Desc.Local(),
        Glyph = Glyphs.Rain,
        DefaultWidth = 300,
        DefaultHeight = 180,
        DescriptionFacts = [("minutes", (long)RainRules.Step.TotalMinutes)],
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Weather, 0, 2),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => RainNextHourSettings.Rows(rows.Rows, (RainNextHourConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new RainNextHourView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RainNextHourConfig))]
internal sealed partial class RainNextHourJsonContext : JsonSerializerContext;
