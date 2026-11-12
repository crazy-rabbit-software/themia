using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.AirFacts;

[assembly: ThemiaWidget("airFacts", typeof(AirFactsModule))]

namespace Themia.Widget.AirFacts;

/// <summary>[S-11135] UV, the pollutants and pollen at a place, as the rows the user picks. SPEC.md beside this file.</summary>
public sealed class AirFactsModule : IWidgetModule
{
    /// <summary>Its glyph, the air index's (U+EC0A, a leaf).</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xEC0A);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("airFacts"),
        ConfigType = typeof(AirFactsConfig),
        CreateDefault = () => new AirFactsConfig(),
        Json = AirFactsJsonContext.Default,
        Keywords = ["pm2.5", "pm10", "particulates", "ozone", "uv", "sunburn", "pollen", "allergy", "hay fever"],
        Label = AirFactsStr.AirFacts_Label.Local(),
        Description = AirFactsStr.AirFacts_Desc.Local(),
        Glyph = Glyph,
        Sizing = c => SkyFacts.Sizing(((AirFactsConfig)c).Facts.Count, AirFactRules.All.Count),
        SizeSets = (c, size) => SkyFacts.SizeSets(c, size, ((AirFactsConfig)c).Facts, AirFactRules.All, (copy, facts) => ((AirFactsConfig)copy).Facts = facts),
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Weather, 0, 5),
    };

    /// <summary>[S-11137] Every option is on the properties menu: the rows, Appearance, the place.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var facts = (AirFactsConfig)config;
        menu.Lines.Add(SkyFactLines.Pick(menu, Glyphs.BulletList, AirFactsStr.AirFacts_Menu_Facts.Text(), facts.Facts, AirFactRules.All, AirFactRules.Name, AirFactsView.FactGlyph));
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.AddRange(WeatherPlaceRows.Lines(menu, facts));
    }

    public FrameworkElement CreateView(IWidgetHost host) => new AirFactsView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AirFactsConfig))]
internal sealed partial class AirFactsJsonContext : JsonSerializerContext;
