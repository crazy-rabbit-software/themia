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
using Themia.Widget.Alarm;

[assembly: ThemiaWidget("alarm", typeof(AlarmModule))]

namespace Themia.Widget.Alarm;

/// <summary>[S-4620] One alarm: a time, the days it repeats on, a name, its sound and snooze, rung by the host's alarm service whether or not the widget is shown. SPEC.md beside this file.</summary>
public sealed class AlarmModule : IWidgetModule
{
    /// <summary>[S-4620] Its glyph, the module's own (U+ECC5): Segoe has no alarm clock, so the clock with its arrow round (Karpo's choice); the bell is the notifications'.</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xECC5);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("alarm"),
        ConfigType = typeof(AlarmConfig),
        CreateDefault = () => new AlarmConfig(),
        Json = AlarmJsonContext.Default,
        Keywords = ["alarm clock", "wake up", "reminder", "ring", "snooze", "morning"],
        Label = AlarmStr.Alarm_Label.Local(),
        Description = AlarmStr.Alarm_Desc.Local(),
        Glyph = Glyph,
        DefaultWidth = 300,
        DefaultHeight = 220,
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Time, 0, 1),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => AlarmSettings.Rows(rows.Rows, (AlarmConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new AlarmView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AlarmConfig))]
internal sealed partial class AlarmJsonContext : JsonSerializerContext;
