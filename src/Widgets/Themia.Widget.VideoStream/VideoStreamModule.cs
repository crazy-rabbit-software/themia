using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell.Config;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk;
using Themia.Widget.VideoStream;

[assembly: ThemiaWidget("videoStream", typeof(VideoStreamModule))]

namespace Themia.Widget.VideoStream;

/// <summary>[S-465] A live camera picture: an MJPEG stream, a snapshot polled on an interval, or a video Windows plays itself. SPEC.md beside this file.</summary>
public sealed class VideoStreamModule : IWidgetModule
{
    /// <summary>Its glyph, the module's own (U+E714).</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xE714);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("videoStream"),
        ConfigType = typeof(VideoStreamConfig),
        CreateDefault = () => new VideoStreamConfig(),
        Json = VideoStreamJsonContext.Default,
        Keywords = ["camera", "webcam", "cctv", "mjpeg", "live view", "printer"],
        Label = VideoStreamStr.WidgetsA_Catalog_VideoStream_Label.Local(),
        Description = VideoStreamStr.WidgetsA_Catalog_VideoStream_Description.Local(),
        Glyph = Glyph,
        DefaultWidth = 320,
        DefaultHeight = 240,
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Media, 1, 1),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => VideoStreamSettings.Rows(rows.Rows, (VideoStreamConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new VideoStreamView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(VideoStreamConfig))]
internal sealed partial class VideoStreamJsonContext : JsonSerializerContext;
