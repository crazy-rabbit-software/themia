using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-760] Where the pictures come from. A closed set.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GallerySource>))]
public enum GallerySource
{
    /// <summary>The pictures in a folder (and, optionally, the folders below it).</summary>
    Folder = 0,

    /// <summary>One picture file.</summary>
    File = 1,

    /// <summary>Bing's photo of the day. Needs no setup.</summary>
    Bing = 2,
}
