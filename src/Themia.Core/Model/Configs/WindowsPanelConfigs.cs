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

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("notifications"),
        ConfigType = typeof(NotificationsConfig),
        CreateDefault = () => new NotificationsConfig(),
        Json = WidgetConfigJsonContext.Default,
        // [S-4400] [S-4440] Windows' own notification list and display brightness.
        Keywords = ["notification center", "action center", "toasts", "alerts", "messages", "clear all"],
        Label = Str.Panels_Notifications_Label.Local(),
        Description = Str.Panels_Notifications_Desc.Local(),
        Glyph = Glyphs.Notifications,
        DefaultWidth = 320,
        DefaultHeight = 320,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.CalendarMail, 2, 0),
    };

    public override WidgetConfig Clone() => new NotificationsConfig();
}

/// <summary>[S-4420] A focus session: a length, and whether it holds Windows' do not disturb while it runs.</summary>
public sealed class FocusSessionConfig : WidgetConfig
{

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("focusSession"),
        ConfigType = typeof(FocusSessionConfig),
        CreateDefault = () => new FocusSessionConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["focus", "focus assist", "do not disturb", "dnd", "timer", "deep work", "concentrate"],
        Label = Str.Panels_Focus_Label.Local(),
        Description = Str.Panels_Focus_Desc.Local(),
        Glyph = Glyphs.Focus,
        DefaultWidth = 240,
        DefaultHeight = 200,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Time, 1, 1),
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
