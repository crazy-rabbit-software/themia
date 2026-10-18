using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>
/// [S-1291] How a file-listing widget opens a file: on a double-click (the default — a widget sits in
/// the pointer's path across the desktop, and one stray click should not launch anything) or on a
/// single click, as the original OneDrive widget did. Walking into a folder is a single click either
/// way: it only changes the widget. Shared by OneDrive and the file explorer.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ItemClickMode>))]
public enum ItemClickMode
{
    Double = 0,
    Single = 1,
}
