using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-4400] Windows' notification list. Nothing to set: the list is Windows' own, and what it shows at a
/// size is the ladder's ([S-4404]), so its menu has no lines of its own ([S-3719]).
/// </summary>
public sealed class NotificationsConfig : WidgetConfig
{
    public override WidgetConfig Clone() => new NotificationsConfig();
}
