namespace Themia.Core.Model;

/// <summary>
/// [S-5525] Which desktop items a view shows: a view's own setting, set in its card on the Views page (the
/// owner's choice of 2026-11-16, "each view chooses"). Own icons ([S-5600]) are placed widgets of the view under
/// every value: the setting governs the desktop's items alone (<see cref="Desktop.DesktopViews"/>).
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ViewDesktopIcons>))]
public enum ViewDesktopIcons
{
    /// <summary>Every desktop item: a new one arrives here too, at the first free spot ([S-5526]). The default.</summary>
    All,

    /// <summary>Only the items picked for this view (an icon placed or kept on it, [S-5527]); a new item never arrives here unless dropped on it.</summary>
    Chosen,

    /// <summary>No desktop item: the view holds no item widget and nothing arrives here.</summary>
    None,
}
