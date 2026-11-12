using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Notifications;

[assembly: ThemiaWidget("notifications", typeof(NotificationsModule))]

namespace Themia.Widget.Notifications;

/// <summary>[S-4400] Windows' notification list, grouped by app as its notification center: open a sender, dismiss one, an app's or all. SPEC.md beside this file.</summary>
public sealed class NotificationsModule : IWidgetModule
{
    /// <summary>Its glyph, the module's own (U+EA8F).</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xEA8F);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("notifications"),
        ConfigType = typeof(NotificationsConfig),
        CreateDefault = () => new NotificationsConfig(),
        Json = NotificationsJsonContext.Default,
        // [S-4400] [S-4440] Windows' own notification list and display brightness.
        Keywords = ["notification center", "action center", "toasts", "alerts", "messages", "clear all"],
        Label = NotificationsStr.Panels_Notifications_Label.Local(),
        Description = NotificationsStr.Panels_Notifications_Desc.Local(),
        Glyph = Glyph,
        Sizing = c => KitSizing.Sizing(((NotificationsConfig)c).RowsShown, addFooter: false),
        SizeSets = (c, size) => KitSizing.SizeSets(c, size, ((NotificationsConfig)c).RowsShown, addFooter: false),
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Links = LinkUse.Of([LinkTopicId.HoveredApp], [LinkTopicId.HoveredApp, LinkTopicId.SelectedApp]),
        Add = new(AddFolderId.CalendarMail, 2, 0),
    };

    /// <summary>[S-11404] Rows, lines per notification, and the app headings.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var notifications = (NotificationsConfig)config;
        menu.Lines.AddRange(KitMenuLines.Rows(menu, notifications));
        menu.Lines.Add(menu.Toggle(null, NotificationsStr.Notifications_AppHeadings.Text(), notifications.AppHeadings, on => notifications.AppHeadings = on));
    }

    public FrameworkElement CreateView(IWidgetHost host) => new NotificationsView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(NotificationsConfig))]
internal sealed partial class NotificationsJsonContext : JsonSerializerContext;
