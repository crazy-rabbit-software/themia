using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-5555] What the desktop's Sort by orders its items by, as Explorer's desktop menu offers. In the SDK so a
/// fence ([S-13013]) keeps its own sort with the same values; the order itself is Core's (<c>DesktopSort</c>).
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DesktopSortBy>))]
public enum DesktopSortBy
{
    /// <summary>By name: the order new items are laid out in (<see cref="DesktopItemOrder"/>).</summary>
    Name,

    /// <summary>By size, smallest first.</summary>
    Size,

    /// <summary>By item type (a file's extension), then by name.</summary>
    ItemType,

    /// <summary>By the date modified, newest first.</summary>
    DateModified,
}
