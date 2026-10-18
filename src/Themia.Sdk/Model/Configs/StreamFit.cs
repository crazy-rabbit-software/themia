using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-466] How a video picture fills the widget. Named for what it does to the picture.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StreamFit>))]
public enum StreamFit
{
    /// <summary>The whole picture, letterboxed. The default: a camera view that crops is a camera view that hides things.</summary>
    Contain = 0,

    /// <summary>Fill the widget and crop what does not fit.</summary>
    Cover = 1,

    /// <summary>Fill both axes and distort.</summary>
    Stretch = 2,
}
