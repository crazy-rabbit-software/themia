using System.Text.Json.Serialization;
using Themia.App.Services;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-14920] What every now-playing part of this family remembers: when it hides ([S-1464], the shared Hide when idle,
/// as the cover's and the progress's). What it shows is its kind.
/// </summary>
public abstract class MusicPartConfig : WidgetConfig, IHidesWhenIdle
{
    /// <summary>[S-1464] When the widget paints nothing at all; edit mode always shows it.</summary>
    public MusicHideWhenIdle HideWhenIdle { get; set; } = MusicHideWhenIdle.Never;

    /// <summary>An unknown hide-when-idle mode reads Never.</summary>
    public override IReadOnlyList<string> Repair()
    {
        if (Enum.IsDefined(HideWhenIdle))
        {
            return [];
        }

        HideWhenIdle = MusicHideWhenIdle.Never;
        return [$"{GetType().Name} had an unknown hide-when-idle mode"];
    }

    /// <summary>A copy of the kind's own type, its settings kept.</summary>
    public override WidgetConfig Clone()
    {
        var copy = Fresh();
        copy.HideWhenIdle = HideWhenIdle;
        return copy;
    }

    /// <summary>A new config of this kind.</summary>
    protected abstract MusicPartConfig Fresh();
}

/// <summary>
/// [S-14921] A text of the track (its title, its artist, its album) on the text base ([S-14934]): a line that sits at the
/// start, the middle or the end of its card ([S-6581]).
/// </summary>
public abstract class MusicTextConfig : MusicPartConfig, IContentAligned;

/// <summary>[S-14921] The song's title: the kind <c>music</c>, the now-playing track atom's id (a placed one is the title now).</summary>
public sealed class MusicConfig : MusicTextConfig
{
    protected override MusicPartConfig Fresh() => new MusicConfig();
}

/// <summary>[S-14921] Who plays it.</summary>
public sealed class MusicArtistConfig : MusicTextConfig
{
    protected override MusicPartConfig Fresh() => new MusicArtistConfig();
}

/// <summary>[S-14921] The album it is from.</summary>
public sealed class MusicAlbumConfig : MusicTextConfig
{
    protected override MusicPartConfig Fresh() => new MusicAlbumConfig();
}

/// <summary>[S-14922] One transport button, the whole tile its button: which one is its kind.</summary>
public abstract class MusicButtonConfig : MusicPartConfig, IDrawsItems
{
    /// <summary>What the button asks of the media session.</summary>
    [JsonIgnore]
    public abstract MediaTransport Transport { get; }

    /// <summary>[S-6573] The tile is an item.</summary>
    bool IDrawsItems.DrawsItems => true;
}

/// <summary>[S-14922] Back a track.</summary>
public sealed class MusicPreviousConfig : MusicButtonConfig
{
    public override MediaTransport Transport => MediaTransport.Previous;

    protected override MusicPartConfig Fresh() => new MusicPreviousConfig();
}

/// <summary>[S-14922] Play or pause.</summary>
public sealed class MusicPlayPauseConfig : MusicButtonConfig
{
    public override MediaTransport Transport => MediaTransport.PlayPause;

    protected override MusicPartConfig Fresh() => new MusicPlayPauseConfig();
}

/// <summary>[S-14922] On a track.</summary>
public sealed class MusicNextConfig : MusicButtonConfig
{
    public override MediaTransport Transport => MediaTransport.Next;

    protected override MusicPartConfig Fresh() => new MusicNextConfig();
}
