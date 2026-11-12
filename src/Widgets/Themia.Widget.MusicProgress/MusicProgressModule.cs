using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.MusicProgress;

[assembly: ThemiaWidget("musicProgress", typeof(MusicProgressModule))]

namespace Themia.Widget.MusicProgress;

/// <summary>[S-11330] How far into what Windows is playing it is: the seek bar between the elapsed time and the total. SPEC.md beside this file.</summary>
public sealed class MusicProgressModule : IWidgetModule
{
    /// <summary>Its glyph (Segoe Fluent Icons U+E7A5, a bar), the module's own.</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xE7A5);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("musicProgress"),
        ConfigType = typeof(MusicProgressConfig),
        CreateDefault = () => new MusicProgressConfig(),
        Json = MusicProgressJsonContext.Default,
        Keywords = ["progress", "seek", "position", "elapsed", "music", "now playing", "song", "track"],
        Label = MusicProgressStr.MusicProgress_Label.Local(),
        Description = MusicProgressStr.MusicProgress_Desc.Local(),
        Glyph = Glyph,
        Sizing = _ => MusicProgressFace.Sizing(),
        Traits = WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.Media, 0, 5),
    };

    /// <summary>[S-11330] Its one option: when it hides ([S-1464]).</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var progress = (MusicProgressConfig)config;
        menu.Lines.Add(menu.Choice(Glyphs.Hide, MusicProgressStr.Music_HideWhenIdle_Label.Text(),
            [
                (MusicHideWhenIdle.Never, MusicProgressStr.Music_HideWhenIdle_Never.Text()),
                (MusicHideWhenIdle.NoSession, MusicProgressStr.WidgetsB_Settings_Music_HideWhenIdle_NoSession.Text()),
                (MusicHideWhenIdle.NotPlaying, MusicProgressStr.WidgetsB_Settings_Music_HideWhenIdle_NotPlaying.Text()),
            ],
            progress.HideWhenIdle, value => progress.HideWhenIdle = value));
    }

    public FrameworkElement CreateView(IWidgetHost host) => new MusicProgressView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MusicProgressConfig))]
internal sealed partial class MusicProgressJsonContext : JsonSerializerContext;
