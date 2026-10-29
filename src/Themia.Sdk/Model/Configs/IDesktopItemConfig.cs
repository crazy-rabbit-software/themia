namespace Themia.Core.Model;

/// <summary>
/// [S-5520] A widget that stands for one desktop item: its config names the item by its key
/// (<see cref="Themia.Sdk.DesktopItemKeys"/>). The host's reconcile reads and rewrites it in every view
/// (a rename, a removal, a new file) without knowing the module that draws it.
/// </summary>
public interface IDesktopItemConfig
{
    /// <summary>The item's key; empty for none.</summary>
    string Item { get; set; }
}
