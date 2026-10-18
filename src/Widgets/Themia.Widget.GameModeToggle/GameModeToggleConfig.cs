using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-3900] Game mode.</summary>
public sealed class GameModeToggleConfig : QuickToggleConfig
{
    /// <summary>The toggle it stands for.</summary>
    [JsonIgnore]
    public override QuickToggle Toggle => QuickToggle.GameMode;

    public override WidgetConfig Clone() => new GameModeToggleConfig { Style = Style };
}
