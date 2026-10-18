using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-3900] Bluetooth.</summary>
public sealed class BluetoothToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.Bluetooth;

    public override WidgetConfig Clone() => new BluetoothToggleConfig { Style = Style };
}
