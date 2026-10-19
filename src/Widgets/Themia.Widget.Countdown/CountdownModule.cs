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
using Themia.Widget.Countdown;

[assembly: ThemiaWidget("countdown", typeof(CountdownModule))]

namespace Themia.Widget.Countdown;

/// <summary>[S-6036] The countdown: one target, its name and the time to go (or gone), in words, a ring or flip cards. SPEC.md beside this file.</summary>
public sealed class CountdownModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("countdown"),
        ConfigType = typeof(CountdownConfig),
        CreateDefault = () => new CountdownConfig(),
        Json = CountdownJsonContext.Default,
        Keywords = ["timer", "days until", "deadline", "event", "anniversary", "birthday"],
        Label = CountdownStr.Widget_Countdown_Label.Local(),
        Description = CountdownStr.WidgetsA_Catalog_Countdown_Description.Local(),
        Glyph = Glyphs.Countdown,
        DefaultWidth = 300,
        DefaultHeight = 200,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Time, 0, 2),
    };

    /// <summary>[S-3719] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => CountdownMenu.Lines(menu, (CountdownConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new CountdownView();
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CountdownConfig))]
internal sealed partial class CountdownJsonContext : JsonSerializerContext;
