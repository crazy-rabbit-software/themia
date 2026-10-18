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
