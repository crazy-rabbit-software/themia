using Themia.Core.Habits;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Music.Tests;

/// <summary>[S-6406] Now playing's styles. Moved with the widget from Core's ListStylesTests, unchanged in what it asserts.</summary>
public sealed class MusicTests
{
    [Fact]
    public void S6406_text_only_draws_and_fetches_no_art()
    {
        Assert.True(new MusicConfig { Style = MusicStyle.Artwork }.ShowsArt);
        Assert.True(new MusicConfig { Style = MusicStyle.Cover }.ShowsArt);
        Assert.False(new MusicConfig { Style = MusicStyle.Text }.ShowsArt);
    }
}
