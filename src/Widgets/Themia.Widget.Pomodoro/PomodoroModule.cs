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
using Themia.Widget.Pomodoro;

[assembly: ThemiaWidget("pomodoro", typeof(PomodoroModule))]

namespace Themia.Widget.Pomodoro;

/// <summary>[S-150] The pomodoro: a focus and break timer, its cue on the flip. SPEC.md beside this file.</summary>
public sealed class PomodoroModule : IWidgetModule
{
    /// <summary>Its glyph, the module's own (U+E945); the break phase shows the timer's.</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xE945);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("pomodoro"),
        ConfigType = typeof(PomodoroConfig),
        CreateDefault = () => new PomodoroConfig(),
        Json = PomodoroJsonContext.Default,
        Keywords = ["timer", "focus", "productivity", "work break", "tomato", "session"],
        Label = PomodoroStr.Widget_Pomodoro_Label.Local(),
        Description = PomodoroStr.WidgetsA_Catalog_Pomodoro_Description.Local(),
        Glyph = Glyph,
        DefaultWidth = 240,
        DefaultHeight = 200,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Time, 1, 0),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => PomodoroSettings.Rows(rows.Rows, (PomodoroConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new PomodoroView();
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PomodoroConfig))]
internal sealed partial class PomodoroJsonContext : JsonSerializerContext;
