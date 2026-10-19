using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-4660] The removable devices: no options, so no lines of its own on its menu ([S-3719]).</summary>
public sealed class RemovableDrivesConfig : WidgetConfig
{
    public override WidgetConfig Clone() => new RemovableDrivesConfig();
}
