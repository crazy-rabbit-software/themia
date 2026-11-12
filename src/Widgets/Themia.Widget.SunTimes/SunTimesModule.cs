using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.SunTimes;

[assembly: ThemiaWidget("sunTimes", typeof(SunTimesModule))]

namespace Themia.Widget.SunTimes;

/// <summary>[S-11115] The sun's times where the observer is, as the rows the user picks. SPEC.md beside this file.</summary>
public sealed class SunTimesModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("sunTimes"),
        ConfigType = typeof(SunTimesConfig),
        CreateDefault = () => new SunTimesConfig(),
        Json = SunTimesJsonContext.Default,
        Keywords = ["sunrise", "sunset", "daylight", "golden hour", "dawn", "dusk", "solar noon", "astronomy"],
        Label = SunTimesStr.SunTimes_Label.Local(),
        Description = SunTimesStr.SunTimes_Desc.Local(),
        Glyph = Glyphs.Sun,
        Sizing = c => SkyFacts.Sizing(((SunTimesConfig)c).Times.Count, SunTimeRules.All.Count),
        SizeSets = (c, size) => SkyFacts.SizeSets(c, size, ((SunTimesConfig)c).Times, SunTimeRules.All, (copy, times) => ((SunTimesConfig)copy).Times = times),
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Weather, 1, 1),
    };

    /// <summary>[S-11117] Every option is on the properties menu: the rows, Appearance, the place.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var times = (SunTimesConfig)config;
        menu.Lines.Add(SkyFactLines.Pick(menu, Glyphs.BulletList, SunTimesStr.SunTimes_Menu_Times.Text(), times.Times, SunTimeRules.All, SunTimeRules.Name, SunTimesView.Glyph));
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.AddRange(SkyPlaceRows.Lines(menu, times));
    }

    public FrameworkElement CreateView(IWidgetHost host) => new SunTimesView();
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SunTimesConfig))]
internal sealed partial class SunTimesJsonContext : JsonSerializerContext;
