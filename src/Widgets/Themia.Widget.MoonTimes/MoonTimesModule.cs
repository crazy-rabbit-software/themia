using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.MoonTimes;

[assembly: ThemiaWidget("moonTimes", typeof(MoonTimesModule))]

namespace Themia.Widget.MoonTimes;

/// <summary>[S-11125] The moon's times where the observer is, as the rows the user picks. SPEC.md beside this file.</summary>
public sealed class MoonTimesModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("moonTimes"),
        ConfigType = typeof(MoonTimesConfig),
        CreateDefault = () => new MoonTimesConfig(),
        Json = MoonTimesJsonContext.Default,
        Keywords = ["moonrise", "moonset", "full moon", "new moon", "lunar", "astronomy"],
        Label = MoonTimesStr.MoonTimes_Label.Local(),
        Description = MoonTimesStr.MoonTimes_Desc.Local(),
        Glyph = Glyphs.Moon,
        Sizing = c => SkyFacts.Sizing(((MoonTimesConfig)c).Times.Count, MoonTimeRules.All.Count),
        SizeSets = (c, size) => SkyFacts.SizeSets(c, size, ((MoonTimesConfig)c).Times, MoonTimeRules.All, (copy, times) => ((MoonTimesConfig)copy).Times = times),
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Weather, 1, 3),
    };

    /// <summary>[S-11127] Every option is on the properties menu: the rows, Appearance, the place.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var times = (MoonTimesConfig)config;
        menu.Lines.Add(SkyFactLines.Pick(menu, Glyphs.BulletList, MoonTimesStr.MoonTimes_Menu_Times.Text(), times.Times, MoonTimeRules.All, MoonTimeRules.Name, _ => Glyphs.Moon));
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.AddRange(SkyPlaceRows.Lines(menu, times));
    }

    public FrameworkElement CreateView(IWidgetHost host) => new MoonTimesView();
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MoonTimesConfig))]
internal sealed partial class MoonTimesJsonContext : JsonSerializerContext;
