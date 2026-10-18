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
using Themia.Widget.HabitTracker;

[assembly: ThemiaWidget("habitTracker", typeof(HabitTrackerModule))]

namespace Themia.Widget.HabitTracker;

/// <summary>[S-1420] One of the habit lists kept on this PC: tick a habit per period, its run and history, in dots, a heatmap or a checklist. SPEC.md beside this file.</summary>
public sealed class HabitTrackerModule : IWidgetModule
{
    /// <summary>Its glyph, the module's own (U+E73E).</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xE73E);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("habitTracker"),
        ConfigType = typeof(HabitTrackerConfig),
        CreateDefault = () => new HabitTrackerConfig(),
        Json = HabitTrackerJsonContext.Default,
        Keywords = ["streaks", "daily", "routine", "goals", "checklist", "tracker"],
        Label = HabitTrackerStr.WidgetsA_Catalog_Habits_Label.Local(),
        Description = HabitTrackerStr.WidgetsA_Catalog_Habits_Description.Local(),
        Glyph = Glyph,
        DefaultWidth = 320,
        DefaultHeight = 240,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Notes, 1, 0),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => HabitTrackerSettings.Rows(rows.Rows, (HabitTrackerConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new HabitTrackerView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(HabitTrackerConfig))]
internal sealed partial class HabitTrackerJsonContext : JsonSerializerContext;
