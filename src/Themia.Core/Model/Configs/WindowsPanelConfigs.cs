using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-4400] Windows' notification list. Nothing to set: the list is Windows' own, and what it shows at a
/// size is the ladder's ([S-4404]), so the widget has no settings page ([S-1750]).
/// </summary>
public sealed class NotificationsConfig : WidgetConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Notifications;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("notifications"),
        Kind = WidgetKind.Notifications,
        ConfigType = typeof(NotificationsConfig),
        CreateDefault = () => new NotificationsConfig(),
        Json = WidgetConfigJsonContext.Default,
        LabelId = Str.Panels_Notifications_Label,
        DescriptionId = Str.Panels_Notifications_Desc,
        Glyph = Glyphs.Notifications,
        DefaultWidth = 320,
        DefaultHeight = 320,
    };

    public override WidgetConfig Clone() => new NotificationsConfig();
}

/// <summary>[S-4420] A focus session: a length, and whether it holds Windows' do not disturb while it runs.</summary>
public sealed class FocusSessionConfig : WidgetConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.FocusSession;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("focusSession"),
        Kind = WidgetKind.FocusSession,
        ConfigType = typeof(FocusSessionConfig),
        CreateDefault = () => new FocusSessionConfig(),
        Json = WidgetConfigJsonContext.Default,
        LabelId = Str.Panels_Focus_Label,
        DescriptionId = Str.Panels_Focus_Desc,
        Glyph = Glyphs.Focus,
        DefaultWidth = 240,
        DefaultHeight = 200,
    };

    /// <summary>[S-4421] How long a session started here runs, in minutes (Windows' own default is 30).</summary>
    public int Minutes { get; set; } = FocusSessionRules.DefaultMinutes;

    /// <summary>[S-4423] Turn on Windows' do not disturb while the session runs, as Windows' Focus does.</summary>
    public bool DoNotDisturb { get; set; } = true;

    public override WidgetConfig Clone() => new FocusSessionConfig { Minutes = Minutes, DoNotDisturb = DoNotDisturb };

    /// <summary>A hand-edited length outside the range is pulled back into it, and said so.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var clamped = FocusSessionRules.ClampMinutes(Minutes);
        if (clamped == Minutes)
        {
            return [];
        }

        var was = Minutes;
        Minutes = clamped;
        return [$"focus session length {was} min was outside {FocusSessionRules.MinMinutes}..{FocusSessionRules.MaxMinutes}; now {clamped}"];
    }
}

/// <summary>[S-4442] Which displays the brightness slider sets.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BrightnessScreens>))]
public enum BrightnessScreens
{
    /// <summary>Every display whose brightness can be set, together, to one level (Karpo's default).</summary>
    All = 0,

    /// <summary>Only the display the widget is on.</summary>
    ThisDisplay = 1,
}

/// <summary>[S-4440] The display brightness, one slider.</summary>
public sealed class BrightnessConfig : WidgetConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Brightness;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("brightness"),
        Kind = WidgetKind.Brightness,
        ConfigType = typeof(BrightnessConfig),
        CreateDefault = () => new BrightnessConfig(),
        Json = WidgetConfigJsonContext.Default,
        LabelId = Str.Panels_Brightness_Label,
        DescriptionId = Str.Panels_Brightness_Desc,
        Glyph = Glyphs.Brightness,
        DefaultWidth = 200,
        DefaultHeight = 150,
    };

    /// <summary>[S-4442] Every display at once (the default), or the one the widget is on.</summary>
    public BrightnessScreens Screens { get; set; } = BrightnessScreens.All;

    public override WidgetConfig Clone() => new BrightnessConfig { Screens = Screens };

    /// <summary>An unknown number in a hand-edited file means the default.</summary>
    public override IReadOnlyList<string> Repair()
    {
        if (Enum.IsDefined(Screens))
        {
            return [];
        }

        var was = Screens;
        Screens = BrightnessScreens.All;
        return [$"brightness screens '{(int)was}' is unknown; setting every display"];
    }
}
