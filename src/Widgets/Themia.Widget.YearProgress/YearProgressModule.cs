using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.YearProgress;

[assembly: ThemiaWidget("yearProgress", typeof(YearProgressModule))]

namespace Themia.Widget.YearProgress;

/// <summary>[S-4800] Year progress: how much of the day, week, month and year has passed. SPEC.md beside this file.</summary>
public sealed class YearProgressModule : IWidgetModule
{
    /// <summary>Its glyph, the module's own (Segoe "DateTime", U+EC92).</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xEC92);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("yearProgress"),
        ConfigType = typeof(YearProgressConfig),
        CreateDefault = () => new YearProgressConfig(),
        Json = YearProgressJsonContext.Default,
        Keywords = ["progress", "percent", "year", "month", "week", "day", "time passed", "elapsed"],
        Label = YearProgressStr.YearProgress_Label.Local(),
        Description = YearProgressStr.YearProgress_Desc.Local(),
        Glyph = Glyph,
        DefaultWidth = 280,
        DefaultHeight = 220,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Time, 2, 0),
    };

    public FrameworkElement CreateView() => new YearProgressView();
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(YearProgressConfig))]
internal sealed partial class YearProgressJsonContext : JsonSerializerContext;
