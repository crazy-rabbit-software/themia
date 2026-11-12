using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.ScreenTime;

[assembly: ThemiaWidget("screenTime", typeof(ScreenTimeModule))]

namespace Themia.Widget.ScreenTime;

/// <summary>[S-11500] The screen-time reading: the period's total and the line under it. SPEC.md beside this file.</summary>
public sealed class ScreenTimeModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("screenTime"),
        ConfigType = typeof(ScreenTimeConfig),
        CreateDefault = () => new ScreenTimeConfig(),
        Json = ScreenTimeJsonContext.Default,
        Keywords = ["usage", "productivity", "time tracking", "screen time", "total", "limit", "statistics"],
        Label = ScreenTimeStr.ScreenTime_Label.Local(),
        Description = ScreenTimeStr.ScreenTime_Desc.Local(),
        Glyph = Glyphs.Hours,
        Sizing = _ => ScreenTimeReading.Sizing(),
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.ScreenTime, 0, 0),
    };

    /// <summary>[S-11502] The period, then the shared limits and hidden apps ([S-3323]).</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var reading = (ScreenTimeConfig)config;
        menu.Lines.Add(ScreenTimeMenuLines.Period(menu, reading.Period, value => reading.Period = value));
        ScreenTimeMenuLines.AddShared(menu, reading, SettingsHost.ScreenTime);
    }

    public FrameworkElement CreateView(IWidgetHost host) => new ScreenTimeView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ScreenTimeConfig))]
internal sealed partial class ScreenTimeJsonContext : JsonSerializerContext;
